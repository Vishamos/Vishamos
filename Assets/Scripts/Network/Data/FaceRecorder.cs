using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Face 값 기록·범위 측정(worklist Data-4, 명세 04 §1). 화면에는 아무것도 그리지 않는다(안전 규칙 1)
public class FaceRecorder : MonoBehaviour
{
    private const int Bins = 1000; // p95를 0.001 단위 히스토그램으로 구해 20분 기록도 메모리가 늘지 않게 한다
    private const float ActiveThreshold = 0.1f;
    private const int Count = (int)EFaceBlendShape.max;

    [SerializeField] private bool recording;

    [Header("확인용 (인스펙터에만 표시)")]
    [SerializeField] private int viewSamples;

    private StreamWriter writer;
    private string basePath;
    private float startTime;
    private int lastFrameId;
    private readonly char[] line = new char[2048];
    private readonly int[,] histogram = new int[Count, Bins];
    private readonly float[] max = new float[Count];
    private readonly double[] sum = new double[Count];
    private readonly int[] activeCount = new int[Count];

    private void Update()
    {
        if (recording && writer == null)
            StartRecording();
        else if (!recording && writer != null)
            StopRecording();

        if (writer == null)
            return;

        FaceReceiver receiver = FaceReceiver.Instance;
        if (!receiver || receiver.FrameId == lastFrameId || receiver.State != EReceiveState.Receiving)
            return;

        lastFrameId = receiver.FrameId;
        Record(receiver.Frame);
    }

    private void OnDisable()
    {
        if (writer != null)
            StopRecording();
        recording = false;
    }

    private void StartRecording()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        basePath = Path.Combine(dir, $"face_{DateTime.Now:yyyyMMdd_HHmmss}");
        try
        {
            Directory.CreateDirectory(dir);
            writer = new StreamWriter(basePath + ".csv", false, new System.Text.UTF8Encoding(false), 65536);
            writer.Write("time_s,seq,time_ms,tracking");
            for (int i = 0; i < Count; i++)
            {
                writer.Write(',');
                writer.Write(((EFaceBlendShape)i).ToString());
            }
            writer.WriteLine();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FaceRecorder] 기록 파일을 만들지 못했습니다. Logs 폴더 권한을 확인하세요: {e.Message}");
            writer?.Dispose();
            writer = null;
            recording = false;
            return;
        }

        Array.Clear(histogram, 0, histogram.Length);
        Array.Clear(max, 0, Count);
        Array.Clear(sum, 0, Count);
        Array.Clear(activeCount, 0, Count);
        viewSamples = 0;
        startTime = Time.realtimeSinceStartup;
        lastFrameId = FaceReceiver.Instance ? FaceReceiver.Instance.FrameId : 0;
        Debug.Log($"[FaceRecorder] 기록 시작: {basePath}.csv");
    }

    private void Record(FaceFrame frame)
    {
        int n = 0;
        n += Format(Time.realtimeSinceStartup - startTime, n);
        line[n++] = ',';
        frame.Seq.TryFormat(line.AsSpan(n), out int w, default, CultureInfo.InvariantCulture);
        n += w;
        line[n++] = ',';
        frame.TimeMs.TryFormat(line.AsSpan(n), out w, default, CultureInfo.InvariantCulture);
        n += w;
        line[n++] = ',';
        line[n++] = frame.Tracking ? '1' : '0';

        for (int i = 0; i < Count; i++)
        {
            line[n++] = ',';
            n += Format(frame.BlendShapes[i], n);
        }
        line[n++] = '\n';

        try
        {
            writer.Write(line, 0, n);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FaceRecorder] 기록 중 쓰기 실패로 중지합니다. 디스크 공간을 확인하세요: {e.Message}");
            recording = false;
            StopRecording();
            return;
        }

        // 추적이 끊긴 프레임은 앱이 보낸 마지막 값이라(명세 01 4.4c) 통계에서 뺀다
        if (!frame.Tracking)
            return;

        for (int i = 0; i < Count; i++)
        {
            float v = frame.BlendShapes[i];
            if (viewSamples == 0 || v > max[i])
                max[i] = v;
            sum[i] += v;
            if (v > ActiveThreshold)
                activeCount[i]++;
            histogram[i, Mathf.Clamp((int)(v * Bins), 0, Bins - 1)]++;
        }
        viewSamples++;
    }

    private int Format(float value, int at)
    {
        value.TryFormat(line.AsSpan(at), out int written, "0.####", CultureInfo.InvariantCulture);
        return written;
    }

    private void StopRecording()
    {
        try
        {
            writer.Dispose();
            using (StreamWriter summary = new StreamWriter(basePath + "_summary.csv", false, new System.Text.UTF8Encoding(false)))
            {
                summary.WriteLine("blendshape,max,p95,mean,ratio_over_0.1,samples");
                for (int i = 0; i < Count; i++)
                {
                    float p95 = viewSamples > 0 ? Mathf.Min(Percentile(i, 0.95f), max[i]) : 0f;
                    double mean = viewSamples > 0 ? sum[i] / viewSamples : 0;
                    double ratio = viewSamples > 0 ? (double)activeCount[i] / viewSamples : 0;
                    summary.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.####},{2:0.###},{3:0.####},{4:0.####},{5}",
                        (EFaceBlendShape)i, max[i], p95, mean, ratio, viewSamples));
                }
            }
            Debug.Log($"[FaceRecorder] 기록 중지: 추적 프레임 {viewSamples}개, 요약 {basePath}_summary.csv");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FaceRecorder] 요약 파일을 쓰지 못했습니다. Logs 폴더 권한·디스크 공간을 확인하세요: {e.Message}");
        }
        writer = null;
    }

    private float Percentile(int index, float p)
    {
        int target = Mathf.CeilToInt(viewSamples * p);
        int cumulative = 0;
        for (int b = 0; b < Bins; b++)
        {
            cumulative += histogram[index, b];
            if (cumulative >= target)
                return (b + 1) / (float)Bins;
        }
        return 1f;
    }
}

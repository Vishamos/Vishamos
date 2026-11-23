using System;
using UnityEngine;


// One Euro Filter (1€ Filter)
// 알고리즘: Casiez, Roussel, Vogel, "1€ Filter: A Simple Speed-based Low-pass Filter
//           for Noisy Input in Interactive Systems", CHI 2012.
// 구현: Claude Code(Anthropic)가 논문 수식으로 작성한 뼈대를 바탕으로 작성. 외부 구현 코드는 복사하지 않음.
// 라이선스: 이 프로젝트(MIT)를 따른다.

namespace MyMath
{
    [Serializable]
    public class OneEuro
    {
        [SerializeField] [Range(0.01f, 5f)] private float minCutoff = 1f;

        //[SerializeField] [Range(0.0f, 10f)] private float beta = 0f;
        [SerializeField] private float dCutoff = 1f;
        float prevX, prevDx;
        bool init;

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }

        public float Filter(float x, float dt, float beta)
        {
            if (!init)
            {
                prevX = x;
                init = true;
                return x;
            }

            if (dt <= 0f) return prevX;

            float dx = (x - prevX) / dt;
            prevDx = Mathf.Lerp(prevDx, dx, Alpha(dCutoff, dt)); // 1. 속도 스무딩 
            float cutoff = minCutoff + beta * Mathf.Abs(prevDx); // 2. 속도 →    cutoff
            prevX = Mathf.Lerp(prevX, x, Alpha(cutoff, dt)); // 3∼4
            return prevX;
        }

        public void Reset() => init = false;
    }


    public class EuroVector3
    {
        private OneEuro x = new();
        private OneEuro y = new();
        private OneEuro z = new();

        public Vector3 FilterVector(float _x, float _y, float _z, float dt, float beta)
        {
            return new Vector3(x.Filter(_x, dt, beta), y.Filter(_y, dt, beta), z.Filter(_z, dt, beta));
        }

        public Vector3 FilterVector(Vector3 pos, float dt, float beta)
        {
            return FilterVector(pos.x, pos.y, pos.z, dt, beta);
        }

        public void Reset()
        {
            x.Reset();
            y.Reset();
            z.Reset();
        }
    }
}
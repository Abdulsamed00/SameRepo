using UnityEngine;

// Geçici ölçüm (11.11): kök sabit hızla ileri gider, animator hızı = v / c; basan ayağın dünyadaki ileri hızı ölçülür.
public class TmpWalkTest : MonoBehaviour
{
    public float v = 1f, c = 1f;
    public float sum, n;
    Animator an; Transform lt, rt; Vector3 pl, pr; bool has; float minL = 9, minR = 9; float t;
    void Start() { an = GetComponent<Animator>(); an.cullingMode = AnimatorCullingMode.AlwaysAnimate; lt = an.GetBoneTransform(HumanBodyBones.LeftToes); rt = an.GetBoneTransform(HumanBodyBones.RightToes); }
    void LateUpdate()
    {
        an.speed = v / c; t += Time.deltaTime;
        transform.position += transform.forward * v * Time.deltaTime;
        Vector3 l = lt.position, r = rt.position;
        minL = Mathf.Min(minL, l.y); minR = Mathf.Min(minR, r.y);
        if (has && t > 1.5f && Time.deltaTime > 0)
        {
            if (l.y - minL < 0.015f) { sum += Vector3.Dot((l - pl) / Time.deltaTime, transform.forward); n++; }
            if (r.y - minR < 0.015f) { sum += Vector3.Dot((r - pr) / Time.deltaTime, transform.forward); n++; }
        }
        pl = l; pr = r; has = true;
    }
}

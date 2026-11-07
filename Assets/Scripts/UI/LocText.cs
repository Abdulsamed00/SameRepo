using TMPro;
using UnityEngine;

/// <summary>
/// <see cref="UiKit.CreateText"/>'in kurduğu yazıya eklenir: dil değişince (<see cref="Loc.Changed"/>) kaynak metni
/// yeniden çevirir. Yazı sonradan kodla başka bir şeye çevrildiyse (son çevirisiyle aynı değilse) dokunmaz; onu
/// yazan kod çevirir.
/// </summary>
public class LocText : MonoBehaviour
{
    private TextMeshProUGUI text;
    private string source;
    private string lastOutput;

    public void Init(TextMeshProUGUI target, string turkish)
    {
        text = target;
        source = turkish;
        lastOutput = Loc.T(turkish);
        text.text = lastOutput;
    }

    private void OnEnable() => Loc.Changed += Retranslate;

    private void OnDisable() => Loc.Changed -= Retranslate;

    private void Retranslate()
    {
        if (text == null || text.text != lastOutput)
            return;
        lastOutput = Loc.T(source);
        text.text = lastOutput;
    }
}

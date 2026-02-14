using UnityEngine;
using TMPro;

public class FontSizeSync : MonoBehaviour
{
    private TextMeshProUGUI[] texts;

    void LateUpdate()
    {
        texts = GetComponentsInChildren<TextMeshProUGUI>();
        
        float minPointSize = float.MaxValue;

        foreach (var txt in texts)
        {
            if (txt.fontSize < minPointSize)
                minPointSize = txt.fontSize;
        }

        foreach (var txt in texts)
        {
            txt.enableAutoSizing = false;
            txt.fontSize = minPointSize;
        }
    }
}
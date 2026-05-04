using UnityEngine;
using TMPro;

public class FontSizeSync : MonoBehaviour
{
    private TextMeshProUGUI[] texts;
    private bool needsRefresh = true;

    void Awake()
    {
        RefreshTexts();
    }

    void OnTransformChildrenChanged()
    {
        needsRefresh = true;
    }

    void LateUpdate()
    {
        if (needsRefresh || texts == null)
            RefreshTexts();
        if (texts == null || texts.Length == 0)
            return;
        
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

    private void RefreshTexts()
    {
        texts = GetComponentsInChildren<TextMeshProUGUI>();
        needsRefresh = false;
    }
}
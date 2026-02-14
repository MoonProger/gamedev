using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance; 
    public GameObject tooltipObject;
    public TextMeshProUGUI tooltipText;

    void Awake() => Instance = this;

    void Update()
    {
        if (tooltipObject.activeSelf)
        {
            tooltipObject.transform.position = Input.mousePosition + new Vector3(70, -30, 0);
        }
    }

    public void Show(string content)
    {
        tooltipObject.SetActive(true);
        tooltipText.text = content;
    }

    public void Hide()
    {
        tooltipObject.SetActive(false);
    }
}
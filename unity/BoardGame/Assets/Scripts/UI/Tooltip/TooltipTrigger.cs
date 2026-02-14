using UnityEngine;
using UnityEngine.EventSystems; 

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string header; 

    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipManager.Instance.Show(header);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.Hide();
    }
}
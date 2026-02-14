using UnityEngine;
using System.Collections.Generic;

public class BoardNode : MonoBehaviour
{
    public string nodeName;
    public List<BoardNode> neighbors; 
    private Renderer rend;
    private Color originalColor;

    void Awake() {
        rend = GetComponent<Renderer>();
        if (rend != null) originalColor = rend.material.color;
    }

    public void SetHighlight(bool side) {
        if (rend == null) return;
        rend.material.color = side ? Color.yellow : originalColor;
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.green;
        if (neighbors == null) return;
        foreach (var n in neighbors) {
            if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }
}
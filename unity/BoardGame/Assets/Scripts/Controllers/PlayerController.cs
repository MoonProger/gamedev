using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{

    [Header("Turn Control")]
public int skipTurns = 0;   
    [Header("Основные данные")]
    public string playerName;
    public BoardNode currentNode;
    public AudioClip jumpSound;

    [Header("Экономика и Прогресс")]
    public int money = 1000;
    public int experience = 0;
    public int success = 0;
    

    [Header("8 Сфер Жизни (0-10)")]
    public int volounteer = 5;
    public int science = 5;
    public int art = 5;
    public int media = 5;
    public int business = 5;
    public int sport = 5;
    public int tourism = 5;
    public int IT = 5;

    [Header("Grants")]
    public List<string> appliedGrants = new List<string>();  // сферы, на которые уже подавал
    public List<string> earnedGrants = new List<string>();   // полученные гранты
    public List<string> completedProjects = new List<string>();

    private Renderer[] renderers;
    private AudioSource audioSource;

    void Awake() 
    {
        renderers = GetComponentsInChildren<Renderer>();
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    #region Visuals & Audio

    public void SetTransparency(bool isTransparent) 
    {
        float alpha = isTransparent ? 0.4f : 1.0f;
        foreach (var r in renderers) 
        {
            foreach (var mat in r.materials) 
            {
                if (mat.HasProperty("_BaseColor")) 
                    mat.SetColor("_BaseColor", new Color(1, 1, 1, alpha));
                else if (mat.HasProperty("_Color")) 
                    mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, alpha);
            }
        }
    }

    public void PlayJumpSound() 
    {
        if (jumpSound != null) audioSource.PlayOneShot(jumpSound);
    }

    public void TeleportToNode(BoardNode node) 
    {
        currentNode = node;
        transform.position = node.transform.position;
    }

    #endregion

    #region Stats Logic

    public void ChangeStat(string statName, int amount)
{
    switch (statName.ToLower())
    {
        case "money": money += amount; break;
        case "experience": experience = Mathf.Clamp(experience + amount, 0, 10); break;
        case "success": success = Mathf.Clamp(success + amount, 0, 12); break;

        case "volounteer": CheckMilestone(ref volounteer, amount); break;
        case "science":    CheckMilestone(ref science,    amount); break;
        case "art":        CheckMilestone(ref art,        amount); break;
        case "media":      CheckMilestone(ref media,      amount); break;
        case "business":   CheckMilestone(ref business,   amount); break;
        case "sport":      CheckMilestone(ref sport,      amount); break;
        case "tourism":    CheckMilestone(ref tourism,    amount); break;
        case "it":         CheckMilestone(ref IT,         amount); break;

        default:
            Debug.LogWarning($"Статистика {statName} не найдена!");
            break;
    }
    Debug.Log($"{playerName}: {statName} {amount}. Текущее: {GetStatValue(statName)}");
}

private void CheckMilestone(ref int stat, int amount)
{
    int before = stat;
    stat = Mathf.Clamp(stat + amount, 0, 10);
    int after = stat;

    // Пересёк порог 5 снизу вверх
    if (before < 5 && after >= 5)
    {
        success = Mathf.Clamp(success + 2, 0, 15);
        Debug.Log($"{playerName}: достиг 5 в сфере — +2 Success (итого {success})");
    }

    // Пересёк порог 10 снизу вверх
    if (before < 10 && after >= 10)
    {
        success = Mathf.Clamp(success + 1, 0, 15);
        Debug.Log($"{playerName}: достиг 10 в сфере — +1 Success (итого {success})");
    }
}

    public int GetStatValue(string statName)
    {
        switch (statName.ToLower())
        {
            case "money": return money;
            case "experience": case "exp": return experience;
            case "success": return success;
            case "volounteer": return volounteer;
            case "science": return science;
            case "art": return art;
            case "media": return media;
            case "business": return business;
            case "sport": return sport;
            case "tourism": return tourism;
            case "it": return IT;
            default: return 0;
        }
    }

    public void RandomizeStats()
{
    volounteer = Random.Range(0, 7);
    science = Random.Range(0, 7);
    art = Random.Range(0, 7);
    media = Random.Range(0, 7);
    business = Random.Range(0, 7);
    sport = Random.Range(0, 7);
    tourism = Random.Range(0, 7);
    IT = Random.Range(0, 7);
    
    Debug.Log($"{playerName}: Сферы рандомизированы.");
}

    #endregion
}
using UnityEngine;

public class PlayerController : MonoBehaviour
{
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
            case "experience": case "exp": experience += amount; break;
            case "success": success += amount; break;

            case "volounteer": volounteer = Mathf.Clamp(volounteer + amount, 0, 10); break;
            case "science":    science    = Mathf.Clamp(science + amount, 0, 10); break;
            case "art":        art        = Mathf.Clamp(art + amount, 0, 10); break;
            case "media":      media      = Mathf.Clamp(media + amount, 0, 10); break;
            case "business":   business   = Mathf.Clamp(business + amount, 0, 10); break;
            case "sport":      sport      = Mathf.Clamp(sport + amount, 0, 10); break;
            case "tourism":    tourism    = Mathf.Clamp(tourism + amount, 0, 10); break;
            case "it":         IT         = Mathf.Clamp(IT + amount, 0, 10); break;

            default:
                Debug.LogWarning($"Статистика {statName} не найдена!");
                break;
        }
        Debug.Log($"{playerName}: {statName} {amount}. Текущее: {GetStatValue(statName)}");
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
    volounteer = Random.Range(1, 11);
    science = Random.Range(1, 11);
    art = Random.Range(1, 11);
    media = Random.Range(1, 11);
    business = Random.Range(1, 11);
    sport = Random.Range(1, 11);
    tourism = Random.Range(1, 11);
    IT = Random.Range(1, 11);
    
    Debug.Log($"{playerName}: Сферы рандомизированы.");
}

    #endregion
}
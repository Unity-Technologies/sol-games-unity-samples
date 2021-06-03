using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Manager : MonoBehaviour
{
    [SerializeField]
    Dropdown m_SceneSelector;

    [SerializeField]
    [Tooltip("Prefab for increasing the scene load factor")]
    GameObject m_LoadFactorPrefab;

    [SerializeField]
    [Tooltip("World position center of the first load factor group")]
    Vector3 m_LoadFactorBasePosition;

    [SerializeField]
    [Min(1)]
    [Tooltip("Each load factor group is a grid of NxN Prefabs")]
    uint m_LoadFactorGridSize = 1;

    [SerializeField]
    [Tooltip("Distance between individual Prefabs in a load factor group")]
    float m_LoadFactorGridOffset;

    [SerializeField]
    [Tooltip("Distance between load factor groups")]
    Vector3 m_LoadFactorGroupOffset;

    [SerializeField]
    Text m_LoadFactorText;
    int m_ActiveUIScene = -1;

    int m_LoadFactor;
    List<GameObject> m_LoadFactorGroups = new List<GameObject>();
    int m_NbUIScenes;

    void Start()
    {
        // Exclude the first scene which is the main scene
        m_NbUIScenes = SceneManager.sceneCountInBuildSettings - 1;
        PopulateSceneDropdown();
        UpdateLoadFactorUI();
        LoadScene(0);
    }

    public void OnIncLoadFactor()
    {
        m_LoadFactor += 1;
        if (m_LoadFactor > m_LoadFactorGroups.Count)
        {
            var loadGroup = new GameObject("Load Group");
            m_LoadFactorGroups.Add(loadGroup);
            var basePosition = m_LoadFactorBasePosition + (m_LoadFactor - 1) * m_LoadFactorGroupOffset;
            loadGroup.transform.position = basePosition;

            float x = -0.5f * (m_LoadFactorGridSize - 1) * m_LoadFactorGridOffset;
            for (int i = 0; i < m_LoadFactorGridSize; i++)
            {
                float y = -0.5f * (m_LoadFactorGridSize - 1) * m_LoadFactorGridOffset;
                for (int j = 0; j < m_LoadFactorGridSize; j++)
                {
                    Instantiate(m_LoadFactorPrefab, basePosition + new Vector3(x, y, 0), Quaternion.identity, loadGroup.transform);
                    y += m_LoadFactorGridOffset;
                }

                x += m_LoadFactorGridOffset;
            }
        }
        else
        {
            m_LoadFactorGroups[m_LoadFactor - 1].SetActive(true);
        }

        UpdateLoadFactorUI();
    }

    public void OnDecLoadFactor()
    {
        if (m_LoadFactor > 0)
        {
            m_LoadFactor -= 1;
            m_LoadFactorGroups[m_LoadFactor].SetActive(false);
        }

        UpdateLoadFactorUI();
    }

    void UpdateLoadFactorUI()
    {
        m_LoadFactorText.text = $"Load factor: {m_LoadFactor}";
    }

    void PopulateSceneDropdown()
    {
        var sceneLabels = new List<string>(m_NbUIScenes);
        for (int i = 0; i < m_NbUIScenes; i++)
        {
            var scenePath = SceneUtility.GetScenePathByBuildIndex(GetUISceneBuildIndex(i));
            var start = scenePath.LastIndexOf('/') + 1;
            scenePath = scenePath.Substring(start, scenePath.LastIndexOf(".") - start);
            sceneLabels.Add(scenePath);
        }

        m_SceneSelector.ClearOptions();
        m_SceneSelector.AddOptions(sceneLabels);
        m_SceneSelector.onValueChanged.AddListener(OnChangeScene);
    }

    void OnChangeScene(int index)
    {
        LoadScene(index);
    }

    void LoadScene(int uiScene)
    {
        if (m_ActiveUIScene != uiScene)
        {
            if (m_ActiveUIScene >= 0)
            {
                SceneManager.UnloadSceneAsync(GetUISceneBuildIndex(m_ActiveUIScene));
            }
            
            SceneManager.LoadSceneAsync(GetUISceneBuildIndex(uiScene), LoadSceneMode.Additive);
            m_ActiveUIScene = uiScene;
        }
    }

    static int GetUISceneBuildIndex(int uiScene)
    {
        // The first scene in the build is the main scene
        return uiScene + 1;
    }
}

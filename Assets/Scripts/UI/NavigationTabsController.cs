using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NavigationTabsController : MonoBehaviour
{
    [Header("Layout Settings")]
    [Tooltip("The parent container holding the actual button tabs (e.g., with a Horizontal Layout Group).")]
    public Transform tabsContainer;

    [Tooltip("Optional override button prefab. If left unassigned, the generator's default button prefab will be used.")]
    public GameObject tabButtonPrefab;

    private List<Button> tabButtons = new List<Button>();
    private List<GameObject> targetPanels = new List<GameObject>();
    private Action<int> onTabSelectedCallback;

    /// <summary>
    /// Initializes the tabs and links them with target content panels.
    /// </summary>
    public void Initialize(List<string> tabNames, List<GameObject> panels, GameObject fallbackButtonPrefab, Action<int> onSelected = null)
    {
        // Use fallbacks if UI properties aren't configured on the prefab
        if (tabsContainer == null)
            tabsContainer = transform;

        // --- ADDED: Automatically configure parent Horizontal Layout Group settings ---
        HorizontalLayoutGroup layoutGroup = tabsContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
        }

        GameObject activePrefab = tabButtonPrefab != null ? tabButtonPrefab : fallbackButtonPrefab;

        // Clear existing generated elements
        foreach (Transform child in tabsContainer)
        {
            if (child.GetComponent<Button>() != null)
            {
                Destroy(child.gameObject);
            }
        }

        tabButtons.Clear();
        targetPanels = panels;
        onTabSelectedCallback = onSelected;

        for (int i = 0; i < tabNames.Count; i++)
        {
            int index = i;
            GameObject btnObj = Instantiate(activePrefab, tabsContainer);

            // --- ADDED: Force correct scale on generation to avoid visual layout offsets ---
            btnObj.transform.localScale = Vector3.one;

            // --- ADDED: Ensure each button has a Layout Element forcing equal flexible width ---
            LayoutElement le = btnObj.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = btnObj.AddComponent<LayoutElement>();
            }
            le.flexibleWidth = 1f;

            Button btn = btnObj.GetComponentInChildren<Button>();

            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = tabNames[i];
                txt.enableAutoSizing = false;
                txt.fontSize = 13f;
                // --- ADDED: Disable word wrapping to stop letters from splitting onto two lines ---
                txt.enableWordWrapping = false;
            }

            if (btn != null)
            {
                btn.onClick.AddListener(() => SelectTab(index));
                tabButtons.Add(btn);
            }
        }

        // Initialize display with the first tab selected
        if (tabNames.Count > 0)
        {
            SelectTab(0);
        }
    }

    /// <summary>
    /// Programmatically switch tabs and update panel visibilities.
    /// </summary>
    public void SelectTab(int index)
    {
        for (int i = 0; i < targetPanels.Count; i++)
        {
            if (targetPanels[i] != null)
            {
                targetPanels[i].SetActive(i == index);
            }
        }

        // Apply interactive feedback to show which tab is active
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null)
            {
                tabButtons[i].interactable = (i != index);
            }
        }

        onTabSelectedCallback?.Invoke(index);
    }
}
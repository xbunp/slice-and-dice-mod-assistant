using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro for modern Unity UI


// =========================================================================
// 1. DATA MODEL & SCHEMA (The Blueprint and The State)
// =========================================================================

public enum FieldType { StringInput, Dropdown, StringList, DropZone }

/// <summary>
/// Defines what a block looks like and how it behaves. 
/// In Unity, this can be a ScriptableObject so you can define blocks in the Inspector natively!
/// </summary>
public class BlockSchema
{
    public string BlockId;
    public string DisplayName;
    public Color CategoryColor;
    public List<FieldSchema> Fields = new List<FieldSchema>();

    // A generic compiler delegate. Inject the compilation logic per schema, completely decoupled from the UI.
    public Func<BlockData, string> CompileLogic;
}

public class FieldSchema
{
    public string FieldId;
    public string Label;
    public FieldType Type;
    public List<string> DropdownOptions; // Used if Type == Dropdown
}

/// <summary>
/// The actual generic state/data payload of a block instance.
/// </summary>
public class BlockData
{
    public BlockSchema Schema;
    public Dictionary<string, string> StringValues = new Dictionary<string, string>();
    public Dictionary<string, List<string>> ListValues = new Dictionary<string, List<string>>();
    public Dictionary<string, List<BlockData>> DropZoneChildren = new Dictionary<string, List<BlockData>>();

    public string Compile() => Schema.CompileLogic?.Invoke(this) ?? "";
}

// =========================================================================
// 2. THE UI COMPONENT (Native-feeling, Self-Compartmentalized Unity UI)
// =========================================================================

/// <summary>
/// The single, reusable UI piece. Attach this to a Canvas Prefab.
/// It dynamically spawns its own fields based on the BlockData provided.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public class VisualBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    public TextMeshProUGUI TitleText;
    public Button ExpandCollapseButton;
    public Button RemoveButton;
    public Transform ContentContainer; // Where inputs and drop zones are spawned
    public Image BackgroundImage;

    [Header("Prefabs (Assign in Inspector)")]
    public GameObject StringInputPrefab;
    public GameObject DropdownPrefab;
    public GameObject StringListPrefab;
    public GameObject DropZonePrefab;

    public BlockData Data { get; private set; }
    private bool _isExpanded = true;

    // Drag and drop state
    private Transform _originalParent;
    private GameObject _placeholder;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;

    public event Action<VisualBlock> OnRemoveRequested;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponentInParent<Canvas>();

        if (ExpandCollapseButton != null)
            ExpandCollapseButton.onClick.AddListener(ToggleExpand);

        if (RemoveButton != null)
            RemoveButton.onClick.AddListener(() => OnRemoveRequested?.Invoke(this));
    }

    /// <summary>
    /// Initializes the UI natively from a generic data structure.
    /// </summary>
    public void Initialize(BlockData data)
    {
        Data = data;
        TitleText.text = data.Schema.DisplayName.ToUpper();
        BackgroundImage.color = data.Schema.CategoryColor;

        // Clear dummy Editor content
        foreach (Transform child in ContentContainer) Destroy(child.gameObject);

        // Generate UI dynamically based on generic Schema
        foreach (var field in data.Schema.Fields)
        {
            GenerateFieldUI(field);
        }
    }

    private void GenerateFieldUI(FieldSchema field)
    {
        GameObject uiElement = null;

        switch (field.Type)
        {
            case FieldType.StringInput:
                uiElement = Instantiate(StringInputPrefab, ContentContainer);
                var input = uiElement.GetComponentInChildren<TMP_InputField>();
                input.text = Data.StringValues.ContainsKey(field.FieldId) ? Data.StringValues[field.FieldId] : "";
                input.onValueChanged.AddListener(val => Data.StringValues[field.FieldId] = val);
                break;

            case FieldType.Dropdown:
                uiElement = Instantiate(DropdownPrefab, ContentContainer);
                var dropdown = uiElement.GetComponentInChildren<TMP_Dropdown>();
                dropdown.ClearOptions();
                dropdown.AddOptions(field.DropdownOptions);
                dropdown.onValueChanged.AddListener(val => Data.StringValues[field.FieldId] = field.DropdownOptions[val]);
                break;

            case FieldType.StringList:
                // Example implementation. Ideally, this prefab handles adding/removing strings and updates the Data.ListValues directly.
                uiElement = Instantiate(StringListPrefab, ContentContainer);
                if (!Data.ListValues.ContainsKey(field.FieldId)) Data.ListValues[field.FieldId] = new List<string>();
                break;

            case FieldType.DropZone:
                uiElement = Instantiate(DropZonePrefab, ContentContainer);
                var dropZone = uiElement.GetComponent<GenericDropZone>();
                dropZone.FieldId = field.FieldId;
                if (!Data.DropZoneChildren.ContainsKey(field.FieldId)) Data.DropZoneChildren[field.FieldId] = new List<BlockData>();
                break;
        }

        // Set dynamic label if the prefab supports it
        var label = uiElement?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null) label.text = field.Label;
    }

    private void ToggleExpand()
    {
        _isExpanded = !_isExpanded;
        ContentContainer.gameObject.SetActive(_isExpanded);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }

    /// <summary>
    /// Reads the UI hierarchy directly to recursively compile child DropZones into the data model.
    /// </summary>
    public void SyncHierarchyToData()
    {
        foreach (var dropZone in ContentContainer.GetComponentsInChildren<GenericDropZone>())
        {
            var childDataList = new List<BlockData>();
            foreach (Transform child in dropZone.transform)
            {
                var visualChild = child.GetComponent<VisualBlock>();
                if (visualChild != null)
                {
                    visualChild.SyncHierarchyToData();
                    childDataList.Add(visualChild.Data);
                }
            }
            Data.DropZoneChildren[dropZone.FieldId] = childDataList;
        }
    }

    // =========================================================================
    // 3. NATIVE DRAG & DROP HANDLING
    // =========================================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        _originalParent = transform.parent;

        _placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(LayoutElement));
        _placeholder.transform.SetParent(_originalParent, false);
        _placeholder.transform.SetSiblingIndex(transform.GetSiblingIndex());

        LayoutElement myLayout = GetComponent<LayoutElement>();
        LayoutElement phLayout = _placeholder.GetComponent<LayoutElement>();
        if (myLayout != null)
        {
            phLayout.preferredWidth = myLayout.preferredWidth;
            phLayout.preferredHeight = myLayout.preferredHeight;
        }

        transform.SetParent(_canvas.transform, true);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.8f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            transform.position = eventData.position;
        else
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector3 worldPoint);
            transform.position = worldPoint;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        GenericDropZone validZone = null;
        foreach (var res in results)
        {
            var zone = res.gameObject.GetComponentInParent<GenericDropZone>();
            if (zone != null && !zone.transform.IsChildOf(this.transform))
            {
                validZone = zone;
                break;
            }
        }

        if (validZone != null)
        {
            if (_placeholder.transform.parent != validZone.transform)
                _placeholder.transform.SetParent(validZone.transform, false);

            UpdatePlaceholderIndex(validZone.transform);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(_placeholder.transform.parent, false);
        transform.SetSiblingIndex(_placeholder.transform.GetSiblingIndex());

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;
        Destroy(_placeholder);

        // Re-sync data hierarchy to reflect the newly dropped UI position
        var rootCanvas = GetComponentInParent<Canvas>();
        foreach (var rootBlock in rootCanvas.GetComponentsInChildren<VisualBlock>())
        {
            if (rootBlock.transform.parent.GetComponent<GenericDropZone>() == null)
                rootBlock.SyncHierarchyToData();
        }
    }

    private void UpdatePlaceholderIndex(Transform targetParent)
    {
        int newIndex = targetParent.childCount;
        for (int i = 0; i < targetParent.childCount; i++)
        {
            Transform child = targetParent.GetChild(i);
            if (child == _placeholder.transform) continue;

            if (transform.position.y > child.position.y)
            {
                newIndex = i;
                if (_placeholder.transform.parent == targetParent && _placeholder.transform.GetSiblingIndex() < newIndex)
                    newIndex--;
                break;
            }
        }
        _placeholder.transform.SetSiblingIndex(newIndex);
    }
}

// =========================================================================
// 4. DROP ZONE (Extremely lightweight target identifier)
// =========================================================================

[RequireComponent(typeof(RectTransform), typeof(VerticalLayoutGroup))]
public class GenericDropZone : MonoBehaviour
{
    public string FieldId; // Matches the Schema ID
}

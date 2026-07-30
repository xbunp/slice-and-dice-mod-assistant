using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AboutUI : RootUI
{
    [Header("Project URLs")]
    public string githubUrl = "https://github.com/xbunp/slice-and-dice-mod-assistant"; // 
    public string gameUrl = "https://store.steampowered.com/app/1775490/Slice__Dice/"; // 
    public string itchUrl = "https://xbunp.itch.io/slice-and-dice-mod-making-assistant";     
    public string discordUrl = "https://discord.gg/TqUdVPSWDt";

    [Header("Useful Community Links")]
    public string rmodSheetUrl = "https://docs.google.com/spreadsheets/d/1gLuZihuASmIEp3gAkGcBOi5elB8nEcOpIj38DcX6GWQ/edit?gid=0#gid=0";
    public string textmodGuideUrl = "https://docs.google.com/document/d/1JUUr5qgPKS1AhcZOwHR8P-DMQID_-BelTvt-i99aicg/edit?tab=t.0#heading=h.304eh3ug7h8h";
    public string almanacUrl = "https://docs.google.com/spreadsheets/d/1hAjtpz4afePzlzwhJCXWM629saGS8SZ2PyvNH1PP3eE/edit?gid=1658347383#gid=1658347383";

    // Entries in this list stay pinned at the top in exact order
    private static readonly List<string> PinnedCredits = new List<string>
    {
        "tann (Slice & Dice Creator)",
        "& All the members of the Slice & Dice Discord Community who helped develop this app:"
    };

    // Entries in this list are automatically sorted alphabetically
    private static readonly List<string> CommunityMembers = new List<string>
    {
        "Thunder",
        "Sefcear",
        "tlaiuwwy",
        "BoplAssassin",
        "_mr.person_",
        "Dog Kisser",
        "Lizaru",
        "Muddz",
        "worldmen",
        "Nano",
        "Cancelion",
        "Leonard Is No Curry",
        "hidekideki",
        "the doc"
    };

    private ScrollRect creditsScrollRect;

    protected override void BuildUIAndBind()
    {
        float canvasHeight = 900f;
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null) canvasHeight = canvasRt.rect.height;
        }

        float scrollHeight = Mathf.Max(canvasHeight - uiGenerator.rowHeight - 80f, 350f);
        string currentVersion = string.IsNullOrEmpty(Application.version) ? "1.0.0" : Application.version;

        // Left Column: App Information & Full Hyperlinks
        var leftRows = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("Header_App", "<b>SLICE & DICE MOD ASSISTANT</b>", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("Lbl_Version", $"<b>Version:</b> v{currentVersion}  |  <b>Platform:</b> {GetPlatformString()}", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer1", "", 1.0f)),
            new GridRowSpec(60f, GridCellSpec.CreateLabel("Lbl_Desc", "A mod-builder assistant app for creating custom heroes, monsters, items, abilities, modifiers, and complete textmods for Slice & Dice, made by xbunp", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer2", "", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("Header_Links", "<b>PROJECT LINKS & RESOURCES</b>", 1.0f))
        };

        // Add Hyperlink rows displaying exact URLs
        AddHyperlinkRow(leftRows, "Slice & Dice", gameUrl, "SliceDice");
        AddHyperlinkRow(leftRows, "GitHub Repo", githubUrl, "GitHub");
        AddHyperlinkRow(leftRows, "Itch.io Page", itchUrl, "Itch");
        AddHyperlinkRow(leftRows, "Community Discord", discordUrl, "Discord");

        // Useful Links Header & Resources
        leftRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer3", "", 1.0f)));
        leftRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("Header_Useful", "<b>USEFUL COMMUNITY GUIDES & SHEETS</b>", 1.0f)));

        AddHyperlinkRow(leftRows, "S&D Almanac (v3.2)", almanacUrl, "Almanac");
        AddHyperlinkRow(leftRows, "Textmod Guide (v3.2)", textmodGuideUrl, "TextmodGuide");
        AddHyperlinkRow(leftRows, "ritemx / rmod Almanac", rmodSheetUrl, "RmodSheet");

        //AddHyperlinkRow(leftRows, "Docs", docsUrl, "Docs");

        // Right Column: Alphabetically Sorted Special Thanks
        var rightRows = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("Header_Thanks", "<b>SPECIAL THANKS TO THE COMMUNITY</b>", 1.0f)),
            new GridRowSpec(scrollHeight, GridCellSpec.CreateScrollView("CreditsScrollView", 1.0f))
        };

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.01f, 0.48f, leftRows),
            new ColumnSpec("Right_Column", 0.52f, 0.99f, rightRows)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        // Style hyperlink buttons to look like native text hyperlinks (transparent background, left-aligned)
        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out var leftRefs))
        {
            RedressHyperlinkButton(leftRefs, "GitHub");
            RedressHyperlinkButton(leftRefs, "SliceDice");
            RedressHyperlinkButton(leftRefs, "Itch");
            RedressHyperlinkButton(leftRefs, "Discord");
            RedressHyperlinkButton(leftRefs, "Docs");
            RedressHyperlinkButton(leftRefs, "RmodSheet");
            RedressHyperlinkButton(leftRefs, "TextmodGuide");
            RedressHyperlinkButton(leftRefs, "Almanac");
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out var rightRefs))
        {
            if (rightRefs.ScrollViews.TryGetValue("CreditsScrollView", out var scrollRect))
            {
                creditsScrollRect = scrollRect;
                PopulateCredits();
            }
        }

        ApplyDynamicLayoutConstraints();
    }

    private void AddHyperlinkRow(List<GridRowSpec> rows, string title, string url, string key)
    {
        string formattedLinkText = $"<color=#4A9EFF><u>{url}</u></color>";

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"Lbl_{key}", $"<b>{title}:</b>", 0.20f),
            GridCellSpec.CreateButton($"Link_{key}", formattedLinkText, 0.65f, () => OpenURL(url)),
            GridCellSpec.CreateButton($"Copy_{key}", "Copy", 0.15f, () => CopyToClipboard(url))
        ));
    }

    private void RedressHyperlinkButton(GridReferences refs, string key)
    {
        if (refs.Buttons.TryGetValue($"Link_{key}", out Button btn))
        {
            // Remove dark button background box so it looks like inline text
            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0, 0, 0, 0);
            }

            // Align link text to the left
            TextMeshProUGUI tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.alignment = TextAlignmentOptions.Left;
                tmpText.fontSize = 13f;
            }
        }
    }

    private void PopulateCredits()
    {
        if (creditsScrollRect == null || uiGenerator == null) return;

        // Combine pinned top credits with alphabetically sorted community credits
        var finalCredits = new List<string>(PinnedCredits);

        finalCredits.AddRange(CommunityMembers.OrderBy(m => m, StringComparer.OrdinalIgnoreCase));

        var rows = new List<GridRowSpec>();
        foreach (var member in finalCredits)
        {
            rows.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Credit_{member}", $"•  {member}", 1.0f)));
        }

        GridReferences refs = uiGenerator.RebuildGrid(creditsScrollRect.content, rows, useMargins: false);
        creditsScrollRect.content.sizeDelta = new Vector2(0f, refs.TotalHeight);
    }

    private string GetPlatformString()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "WebGL Applet (Itch.io)";
#elif UNITY_EDITOR
        return "Unity Editor";
#else
        return "Desktop Standalone";
#endif
    }

    private void OpenURL(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Application.OpenURL(url);
        }
    }

    private void ApplyDynamicLayoutConstraints()
    {
        if (creditsScrollRect != null)
        {
            RectTransform scrollRt = creditsScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            if (rowRt != null)
            {
                var layoutElement = rowRt.GetComponent<LayoutElement>() ?? rowRt.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = -1;
                layoutElement.flexibleHeight = 1f;

                rowRt.anchorMin = new Vector2(0f, 0f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.offsetMin = new Vector2(0f, 10f);
                rowRt.offsetMax = new Vector2(0f, -(uiGenerator.rowHeight + 15f));
            }

            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
        }
    }

    private void CopyToClipboard(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        GUIUtility.systemCopyBuffer = url;
        uiGenerator?.CreatePopup("URL copied to clipboard!", true, null);
    }
}
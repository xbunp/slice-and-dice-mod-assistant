using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ModifierUI : RootUI
{
    private ModifierData _activeModifier;
    private RectTransform _payloadColumnPanel;

    // --- ENFORCED DROPDOWN OPTIONS ---
    // Mapped precisely to the game's accepted aliases
    private readonly string[] _phaseOptions = { "None", "ch", "ph", "phi", "phmp", "fh", "lh" };
    private readonly string[] _phaseNiceNames = { "None", "Choosable (ch)", "Phase (ph)", "Phase Indexed (phi)", "Phase Mod Pick (phmp)", "First Half (fh)", "Last Half (lh)" };

    private readonly string[] _heroPosOptions = { "None", "top", "bot", "mid", "mid3", "topbot", "top2", "top3", "top4", "bot2", "bot3", "bot4", "eo" };
    private readonly string[] _heroPosNiceNames = { "None", "Top", "Bottom", "Middle", "Middle 3", "Top & Bottom", "Top 2", "Top 3", "Top 4", "Bottom 2", "Bottom 3", "Bottom 4", "Every Other (eo)" };

    private readonly string[] _diceTargetOptions = { "None", "all", "left", "middle", "right", "top", "bottom", "rightmost", "row", "col", "even", "odd", "corners" };
    private readonly string[] _diceTargetNiceNames = { "None", "All Faces", "Left", "Middle", "Right", "Top", "Bottom", "Rightmost", "Row", "Column", "Even", "Odd", "Corners" };

    private readonly string[] _difficultyOptions = { "None", "Heaven", "Easy", "Normal", "Hard", "Unfair", "Brutal", "Hell" };

    private readonly string[] _actionTypeNiceNames = {
        "Core Effect (e.g. Cantrip)", "Add Monster", "Add Hero", "Give Item",
        "Give All Items", "Give All Items (Equipped)", "Per Item Effect", "Delivery (Seed)",
        "RMod (Seed)", "End Turn Ability", "Transform Hero", "Party Heroes",
        "Monster Spirit", "Jinx", "Vase", "Self", "Inline Monster", "Inline Hero",
        "Choosable", "Phase", "Phase Indexed", "Phase Mod Pick"
    };

    protected override void BuildUIAndBind()
    {
        _activeModifier = ModPackage.Instance?.GetActiveEntity<ModifierData>();
        if (_activeModifier == null)
        {
            _activeModifier = new ModifierData();
        }

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            new ColumnSpec("TimingCol", 0.0f, 0.33f, BuildTimingAndTargetingRows()),
            new ColumnSpec("PayloadCol", 0.33f, 0.66f, BuildPayloadRows()),
            new ColumnSpec("MetaCol", 0.66f, 1.0f, BuildMetaAndOutputRows())
        };

        generatedScreen = uiGenerator.SetupScreen(columns, true);

        if (generatedScreen.ColumnPanels.TryGetValue("PayloadCol", out RectTransform payloadPanel))
        {
            _payloadColumnPanel = payloadPanel;
        }

        // Extremely important: This syncs the UI visually to the data defaults (preventing toggles from showing 'ON' initially)
        SyncUIToData();
        RefreshOutput();
    }

    private void OnDataChanged()
    {
        ModPackage.Instance?.UpdateActiveEntityClone(_activeModifier);
        EnforceExclusivityRules();
        RefreshOutput();
    }

    /// <summary>
    /// Actively sanitizes and strips conflicting data out of the Modifier so the user cannot crash the parser
    /// </summary>
    private void EnforceExclusivityRules()
    {
        // Targetless Actions shouldn't possess target scopes
        bool isTargetless = _activeModifier.ActionType == ModifierActionType.AddMonster ||
                            _activeModifier.ActionType == ModifierActionType.AddHero ||
                            _activeModifier.ActionType == ModifierActionType.PartyHeroes ||
                            _activeModifier.ActionType == ModifierActionType.Delivery ||
                            _activeModifier.ActionType == ModifierActionType.RMod;

        if (isTargetless)
        {
            bool needsSync = _activeModifier.InvertTarget || _activeModifier.HeroPosition != null ||
                             _activeModifier.TargetAllHeroes || _activeModifier.TargetAllMonsters ||
                             _activeModifier.DiceFaceTarget != null;

            _activeModifier.InvertTarget = false;
            _activeModifier.HeroPosition = null;
            _activeModifier.TargetAllHeroes = false;
            _activeModifier.TargetAllMonsters = false;
            _activeModifier.DiceFaceTarget = null;

            if (needsSync) SyncUIToData();
        }

        // Transform Hero specifically targets Heroes, not monsters
        if (_activeModifier.ActionType == ModifierActionType.TransformHero && _activeModifier.TargetAllMonsters)
        {
            _activeModifier.TargetAllMonsters = false;
            SyncUIToData();
        }
    }

    // =========================================================================================
    // COLUMN 1: TIMING, STACKING & TARGETING
    // =========================================================================================
    private List<GridRowSpec> BuildTimingAndTargetingRows()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- TIMING & CADENCE ---", 1.0f)));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Floors:", 0.3f),
            GridCellSpec.CreateInput("FloorLevel", "e.g. 1 or 1-5", 0.7f, (val) => { _activeModifier.FloorLevel = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Turn:", 0.3f),
            GridCellSpec.CreateInput("Turn", "Number only (e.g. 1)", 0.7f, (val) => {
                string clean = val.Replace("t", "").Trim();
                _activeModifier.Turn = string.IsNullOrEmpty(clean) ? null : "t" + clean;
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Phase:", 0.3f),
            GridCellSpec.CreateDropdown("PhaseDrop", "", 0.7f, _phaseNiceNames, (idx) => {
                _activeModifier.Phase = idx == 0 ? null : _phaseOptions[idx];
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Ev. X Fights:", 0.35f),
            GridCellSpec.CreateInput("EveryXFights", "Number (e.g. 2)", 0.35f, (val) => {
                string clean = val.Replace("e", "").Trim();
                _activeModifier.EveryXFights = string.IsNullOrEmpty(clean) ? null : "e" + clean;
                OnDataChanged();
            }),
            GridCellSpec.CreateInput("EveryXFightsOffset", "Offset (e.g. 1)", 0.30f, (val) => {
                string clean = val.Replace(".", "").Trim();
                _activeModifier.EveryXFightsOffset = string.IsNullOrEmpty(clean) ? null : "." + clean;
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Ev. X Turns:", 0.3f),
            GridCellSpec.CreateInput("EveryXTurns", "Number (e.g. 3)", 0.7f, (val) => {
                string clean = val.Replace("et", "").Trim();
                _activeModifier.EveryXTurns = string.IsNullOrEmpty(clean) ? null : "et" + clean;
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- STACKING & TARGETING ---", 1.0f)));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Repeat (x):", 0.3f),
            GridCellSpec.CreateInput("RepeatTimes", "Number (e.g. 3)", 0.7f, (val) => {
                string clean = val.Replace("x", "").Trim();
                _activeModifier.RepeatTimes = string.IsNullOrEmpty(clean) ? null : "x" + clean;
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateToggle("PerFightStack", "Per Fight (pl)", 0.33f, (val) => { _activeModifier.PerFightStack = val; OnDataChanged(); }),
            GridCellSpec.CreateToggle("PerBossStack", "Per Boss (pb)", 0.33f, (val) => { _activeModifier.PerBossStack = val; OnDataChanged(); }),
            GridCellSpec.CreateToggle("PerTurnStack", "Per Turn (pt)", 0.33f, (val) => { _activeModifier.PerTurnStack = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Dice Target:", 0.3f),
            GridCellSpec.CreateDropdown("DiceFaceTargetDrop", "", 0.7f, _diceTargetNiceNames, (idx) => {
                _activeModifier.DiceFaceTarget = idx == 0 ? null : _diceTargetOptions[idx];
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Pos:", 0.3f),
            GridCellSpec.CreateDropdown("HeroPosDrop", "", 0.7f, _heroPosNiceNames, (idx) => {
                _activeModifier.HeroPosition = idx == 0 ? null : _heroPosOptions[idx];
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateToggle("TargetAllHeroes", "All Heroes", 0.5f, (val) => { _activeModifier.TargetAllHeroes = val; OnDataChanged(); }),
            GridCellSpec.CreateToggle("TargetAllMonsters", "All Monsters", 0.5f, (val) => { _activeModifier.TargetAllMonsters = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateToggle("InvertTarget", "Invert Target (inv)", 0.5f, (val) => { _activeModifier.InvertTarget = val; OnDataChanged(); }),
            GridCellSpec.CreateToggle("Unpack", "Unpack List", 0.5f, (val) => { _activeModifier.Unpack = val; OnDataChanged(); })
        ));

        return rows;
    }

    // =========================================================================================
    // COLUMN 2: DYNAMIC PAYLOAD ACTION
    // =========================================================================================
    private List<GridRowSpec> BuildPayloadRows()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- ACTION PAYLOAD ---", 1.0f)));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Action Type:", 0.3f),
            GridCellSpec.CreateFilteredDropdown("ActionTypeDrop", "", 0.7f, _actionTypeNiceNames, (idx) => {
                _activeModifier.ActionType = (ModifierActionType)idx;
                RebuildPayloadColumn();
                OnDataChanged();
            })
        ));

        switch (_activeModifier.ActionType)
        {
            case ModifierActionType.CoreModifier:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Core Effect:", 0.3f),
                    GridCellSpec.CreateInput("CoreEffectName", "e.g. cantrip, pain, Shield Response", 0.7f, (val) => { _activeModifier.CoreEffectName = val; OnDataChanged(); })
                ));
                break;

            case ModifierActionType.GiveItem:
            case ModifierActionType.AllItem:
            case ModifierActionType.AllItemE:
            case ModifierActionType.PerItem:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Raw Item Data:", 0.3f),
                    GridCellSpec.CreateInput("RawItemPayload", "Item parse string...", 0.7f, (val) => {
                        _activeModifier.ItemPayload = new ItemData();
                        _activeModifier.ItemPayload.Parse(val);
                        OnDataChanged();
                    })
                ));
                break;

            case ModifierActionType.AddMonster:
            case ModifierActionType.InlineMonster:
            case ModifierActionType.MonsterSpirit:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Monster Data:", 0.3f),
                    GridCellSpec.CreateInput("RawMonsterPayload", "Monster parse string...", 0.7f, (val) => {
                        _activeModifier.MonsterPayload = new MonsterData();
                        _activeModifier.MonsterPayload.Parse(val);
                        OnDataChanged();
                    })
                ));
                break;

            case ModifierActionType.AddHero:
            case ModifierActionType.InlineHero:
            case ModifierActionType.TransformHero:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hero Data:", 0.3f),
                    GridCellSpec.CreateInput("RawHeroPayload", "Hero parse string...", 0.7f, (val) => {
                        _activeModifier.HeroPayload = new HeroData();
                        _activeModifier.HeroPayload.Parse(val);
                        OnDataChanged();
                    })
                ));
                break;

            case ModifierActionType.EndTurnAbility:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Ability Data:", 0.3f),
                    GridCellSpec.CreateInput("RawAbilityPayload", "Ability parse string...", 0.7f, (val) => {
                        _activeModifier.AbilityPayload = AbilityData.CreateAbility(val);
                        OnDataChanged();
                    })
                ));
                break;

            case ModifierActionType.Jinx:
            case ModifierActionType.Vase:
            case ModifierActionType.Self:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Nested Modifier:", 0.3f),
                    GridCellSpec.CreateInput("NestedModPayload", "Nested mod string...", 0.7f, (val) => {
                        _activeModifier.NestedModifierPayload = new ModifierData();
                        _activeModifier.NestedModifierPayload.Parse(val);
                        OnDataChanged();
                    })
                ));
                break;

            case ModifierActionType.Choosable:
            case ModifierActionType.Phase:
            case ModifierActionType.PhaseIndexed:
            case ModifierActionType.PhaseModPick:
            case ModifierActionType.Delivery:
            case ModifierActionType.RMod:
            case ModifierActionType.PartyHeroes:
                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("String Payload:", 0.3f),
                    GridCellSpec.CreateInput("StringPayload", "Context-dependent string...", 0.7f, (val) => {
                        _activeModifier.StringPayload = val;
                        OnDataChanged();
                    })
                ));
                break;
        }

        return rows;
    }

    private void RebuildPayloadColumn()
    {
        if (_payloadColumnPanel == null) return;
        var gridRefs = uiGenerator.RebuildGrid(_payloadColumnPanel, BuildPayloadRows(), true);
        generatedScreen.ColumnRefs["PayloadCol"] = gridRefs;
        SyncUIToData(); // Resync default values in the new input elements
    }

    // =========================================================================================
    // COLUMN 3: META, COMBINATORS & OUTPUT
    // =========================================================================================
    private List<GridRowSpec> BuildMetaAndOutputRows()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- SUFFIXES & META ---", 1.0f)));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Target Part:", 0.3f),
            GridCellSpec.CreateInput("PartIndex", "Index (e.g. 0)", 0.7f, (val) => {
                if (int.TryParse(val, out int result)) _activeModifier.PartIndex = result;
                else _activeModifier.PartIndex = null;
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Mod Tier:", 0.3f),
            GridCellSpec.CreateInput("ModTier", "Number (e.g. 1)", 0.7f, (val) => { _activeModifier.ModTier = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Difficulty:", 0.3f),
            GridCellSpec.CreateDropdown("DifficultyDrop", "", 0.7f, _difficultyOptions, (idx) => {
                _activeModifier.Difficulty = idx == 0 ? null : _difficultyOptions[idx];
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Mod Name:", 0.3f),
            GridCellSpec.CreateInput("ModName", "Visible Modifier Name", 0.7f, (val) => { _activeModifier.ModName = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Description:", 0.3f),
            GridCellSpec.CreateInput("DocDesc", "Tooltip text...", 0.7f, (val) => { _activeModifier.DocDescription = val; OnDataChanged(); })
        ));

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- COMBINATORS ---", 1.0f)));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Chained (&):", 0.3f),
            GridCellSpec.CreateInput("ChainedMod", "Raw modifier string...", 0.7f, (val) => {
                if (string.IsNullOrWhiteSpace(val)) _activeModifier.ChainedModifier = null;
                else
                {
                    _activeModifier.ChainedModifier = new ModifierData();
                    _activeModifier.ChainedModifier.Parse(val);
                }
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Spliced (.splice.):", 0.3f),
            GridCellSpec.CreateInput("SplicedMod", "Raw modifier string...", 0.7f, (val) => {
                if (string.IsNullOrWhiteSpace(val)) _activeModifier.SplicedModifier = null;
                else
                {
                    _activeModifier.SplicedModifier = new ModifierData();
                    _activeModifier.SplicedModifier.Parse(val);
                }
                OnDataChanged();
            })
        ));

        rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("--- COMPILER OUTPUT ---", 1.0f)));

        // Output Display Area (Read-Only visually)
        rows.Add(new GridRowSpec(
            80f,
            GridCellSpec.CreateInput("OutputPreview", "Output will appear here...", 1.0f, null, InputAlignment.Top)
        ));

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnCopyOutput", "Copy to Clipboard", 1.0f, () => {
                string exportStr = GetExportStringSafely();
                GUIUtility.systemCopyBuffer = exportStr;
                uiGenerator.CreatePopup("Modifier string copied to clipboard!");
            })
        ));

        return rows;
    }

    /// <summary>
    /// Forces UI elements to mirror the exact state of the loaded ModifierData.
    /// Safely strips engine prefixes to maintain human-readable inputs.
    /// </summary>
    private void SyncUIToData()
    {
        if (generatedScreen == null || _activeModifier == null) return;

        foreach (var col in generatedScreen.ColumnRefs.Values)
        {
            // Sync Text Inputs (Strip Engine Prefixes for UI formatting)
            if (col.Inputs.TryGetValue("FloorLevel", out var dFlr)) dFlr.SetTextWithoutNotify(_activeModifier.FloorLevel ?? "");
            if (col.Inputs.TryGetValue("Turn", out var dTurn)) dTurn.SetTextWithoutNotify(_activeModifier.Turn?.Replace("t", "") ?? "");
            if (col.Inputs.TryGetValue("EveryXFights", out var dExf)) dExf.SetTextWithoutNotify(_activeModifier.EveryXFights?.Replace("e", "") ?? "");
            if (col.Inputs.TryGetValue("EveryXFightsOffset", out var dExfo)) dExfo.SetTextWithoutNotify(_activeModifier.EveryXFightsOffset?.Replace(".", "") ?? "");
            if (col.Inputs.TryGetValue("EveryXTurns", out var dExt)) dExt.SetTextWithoutNotify(_activeModifier.EveryXTurns?.Replace("et", "") ?? "");
            if (col.Inputs.TryGetValue("RepeatTimes", out var dRep)) dRep.SetTextWithoutNotify(_activeModifier.RepeatTimes?.Replace("x", "") ?? "");

            if (col.Inputs.TryGetValue("PartIndex", out var dPart)) dPart.SetTextWithoutNotify(_activeModifier.PartIndex?.ToString() ?? "");
            if (col.Inputs.TryGetValue("ModTier", out var dTier)) dTier.SetTextWithoutNotify(_activeModifier.ModTier ?? "");
            if (col.Inputs.TryGetValue("ModName", out var dMn)) dMn.SetTextWithoutNotify(_activeModifier.ModName ?? "");
            if (col.Inputs.TryGetValue("DocDesc", out var dDoc)) dDoc.SetTextWithoutNotify(_activeModifier.DocDescription ?? "");

            // Dynamic Payload Input Syncing
            if (col.Inputs.TryGetValue("CoreEffectName", out var dCore)) dCore.SetTextWithoutNotify(_activeModifier.CoreEffectName ?? "");
            if (col.Inputs.TryGetValue("StringPayload", out var dStr)) dStr.SetTextWithoutNotify(_activeModifier.StringPayload ?? "");
            if (col.Inputs.TryGetValue("RawItemPayload", out var dItem)) dItem.SetTextWithoutNotify(_activeModifier.ItemPayload?.Export() ?? "");
            if (col.Inputs.TryGetValue("RawMonsterPayload", out var dMon)) dMon.SetTextWithoutNotify(_activeModifier.MonsterPayload?.Export() ?? "");
            if (col.Inputs.TryGetValue("RawHeroPayload", out var dHero)) dHero.SetTextWithoutNotify(_activeModifier.HeroPayload?.Export() ?? "");
            if (col.Inputs.TryGetValue("NestedModPayload", out var dNest)) dNest.SetTextWithoutNotify(_activeModifier.NestedModifierPayload?.ExportInternal(false) ?? "");

            // Sync Toggles (Defaults to OFF visually, neutralizing Unity's true-by-default behavior)
            if (col.Toggles.TryGetValue("PerFightStack", out var tPf)) tPf.SetIsOnWithoutNotify(_activeModifier.PerFightStack);
            if (col.Toggles.TryGetValue("PerBossStack", out var tPb)) tPb.SetIsOnWithoutNotify(_activeModifier.PerBossStack);
            if (col.Toggles.TryGetValue("PerTurnStack", out var tPt)) tPt.SetIsOnWithoutNotify(_activeModifier.PerTurnStack);
            if (col.Toggles.TryGetValue("TargetAllHeroes", out var tAh)) tAh.SetIsOnWithoutNotify(_activeModifier.TargetAllHeroes);
            if (col.Toggles.TryGetValue("TargetAllMonsters", out var tAm)) tAm.SetIsOnWithoutNotify(_activeModifier.TargetAllMonsters);
            if (col.Toggles.TryGetValue("InvertTarget", out var tInv)) tInv.SetIsOnWithoutNotify(_activeModifier.InvertTarget);
            if (col.Toggles.TryGetValue("Unpack", out var tUnk)) tUnk.SetIsOnWithoutNotify(_activeModifier.Unpack);

            // Sync Validated Dropdowns
            if (col.Dropdowns.TryGetValue("PhaseDrop", out var dropPhase))
            {
                int index = Array.IndexOf(_phaseOptions, _activeModifier.Phase);
                dropPhase.SetValueWithoutNotify(index >= 0 ? index : 0);
                dropPhase.RefreshShownValue();
            }
            if (col.Dropdowns.TryGetValue("HeroPosDrop", out var dropPos))
            {
                int index = Array.IndexOf(_heroPosOptions, _activeModifier.HeroPosition);
                dropPos.SetValueWithoutNotify(index >= 0 ? index : 0);
                dropPos.RefreshShownValue();
            }
            if (col.Dropdowns.TryGetValue("DiceFaceTargetDrop", out var dropDice))
            {
                int index = Array.IndexOf(_diceTargetOptions, _activeModifier.DiceFaceTarget);
                dropDice.SetValueWithoutNotify(index >= 0 ? index : 0);
                dropDice.RefreshShownValue();
            }
            if (col.Dropdowns.TryGetValue("DifficultyDrop", out var dropDiff))
            {
                int index = Array.IndexOf(_difficultyOptions, _activeModifier.Difficulty);
                dropDiff.SetValueWithoutNotify(index >= 0 ? index : 0);
                dropDiff.RefreshShownValue();
            }
            if (col.FilteredDropdowns.TryGetValue("ActionTypeDrop", out var dropType))
            {
                dropType.SetValueWithoutNotify((int)_activeModifier.ActionType);
                dropType.RefreshShownValue();
            }
        }
    }

    /// <summary>
    /// Refreshes the Live Compiler Output box, catching validation rules and structural errors.
    /// </summary>
    private void RefreshOutput()
    {
        if (generatedScreen == null || !generatedScreen.ColumnRefs.ContainsKey("MetaCol")) return;

        var metaRefs = generatedScreen.ColumnRefs["MetaCol"];
        if (metaRefs.Inputs.TryGetValue("OutputPreview", out TMP_InputField outputField))
        {
            string exportResult = GetExportStringSafely();

            // Highlight errors in red if validation failed
            if (exportResult.StartsWith("ERROR:"))
            {
                outputField.textComponent.color = new Color(1f, 0.4f, 0.4f); // Red text
            }
            else if (exportResult == "Waiting for input...")
            {
                outputField.textComponent.color = new Color(0.6f, 0.6f, 0.6f); // Grey prompt
            }
            else
            {
                outputField.textComponent.color = Color.white;
            }

            outputField.SetTextWithoutNotify(exportResult);
        }
    }

    private string GetExportStringSafely()
    {
        try
        {
            // Execute validation and generation logic
            string compiledString = _activeModifier.ExportInternal(true);

            // Allow C#'s strict validations to process (e.g. flagging 'diff' combinations if backend demands it)
            _activeModifier.Validate(true);

            // Prevent returning ugly empty brackets
            if (string.IsNullOrWhiteSpace(compiledString) || compiledString == "()")
                return "Waiting for input...";

            // Re-inject Difficulty globally if specified (Since it's handled distinctively from normal parts by textmod standards)
            if (!string.IsNullOrEmpty(_activeModifier.Difficulty))
                compiledString = $"diff.{_activeModifier.Difficulty}.{compiledString}".TrimEnd('.');

            return compiledString;
        }
        catch (InvalidOperationException ex)
        {
            // Catches expected engine validation constraints
            return $"ERROR: {ex.Message}";
        }
        catch (Exception ex)
        {
            // Catches catastrophic structural failure in syntax
            return $"ERROR: Syntax Failure - {ex.Message}";
        }
    }
}
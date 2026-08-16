using SliceAndDice.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SliceAndDice.Compiler
{
    #region --- DOMAIN ENUMS ---

    /// <summary>
    /// Represents the 6 registers (faces) of a die, plus 'All' for broadcast operations.
    /// Order: Left, Middle, Top, Bottom, Right, Rightmost.
    /// </summary>
    public enum Face
    {
        Left,       // Register 0 (Accumulator / AX)
        Middle,     // Register 1 (Default Hat Return / Scratchpad)
        Top,        // Register 2
        Bottom,     // Register 3
        Right,      // Register 4
        Rightmost,  // Register 5
        All         // Broadcast Macro
    }

    public enum SpecialFaceType
    {
        None,
        Sticker,
        Cast,
        Enchant,
        Egg
    }

    public class UntargetedEffectSpec
    {
        public HatPayload SourceEffect { get; set; }

        /// <summary>
        /// If the source effect is targeted (e.g. Single Damage), this defines the untargeted 
        /// scope to convert it to before executing 'togunt'. 
        /// Options: AllEnemies, AllAllies, Everyone, Self.
        /// </summary>
        public TargetScope UntargetedConversionScope { get; set; } = TargetScope.AllEnemies;

        /// <summary>
        /// Set to true for inherently untargeted payloads (Mana, Reroll, Revive, Enchant, Egg).
        /// Bypasses targeting transformation.
        /// </summary>
        public bool IsInherentlyUntargeted { get; set; } = false;

        public UntargetedEffectSpec(
            HatPayload sourceEffect,
            TargetScope conversionScope = TargetScope.AllEnemies,
            bool isInherentlyUntargeted = false)
        {
            SourceEffect = sourceEffect;
            UntargetedConversionScope = conversionScope;
            IsInherentlyUntargeted = isInherentlyUntargeted;
        }
    }

    public class SpecialFaceSpec
    {
        public SpecialFaceType Type { get; set; }
        public string Payload { get; set; } = string.Empty;
        public TargetScope TargetScope { get; set; } = TargetScope.SingleAlly;
        public string Facade { get; set; } = string.Empty;

        /// <summary>
        /// Applicable only to Egg faces. 
        /// Default is false, which appends '#blindfold' to strip the native 'death' keyword.
        /// </summary>
        public bool KeepDeathKeyword { get; set; } = false;

        public SpecialFaceSpec(
            SpecialFaceType type,
            string payload,
            TargetScope targetScope = TargetScope.SingleAlly,
            string facade = "",
            bool keepDeathKeyword = false)
        {
            Type = type;
            Payload = payload;
            TargetScope = targetScope;
            Facade = facade;
            KeepDeathKeyword = keepDeathKeyword;
        }
    }

    /// <summary>
    /// Represents properties propagated via 'tog' items from the Left face (Accumulator).
    /// </summary>
    public enum TogType
    {
        Targeting,  // togtarg
        Visuals,    // togvis
        Effect,     // togeft
        Pips,       // togpip
        Keywords,   // togkey
        OrEffect,   // togorf
        Untargeted, // togunt

        // Logic Gate Restrictions
        Restriction,  // togres
        RestrictionM, // togresm
        RestrictionA, // togresa
        RestrictionO, // togreso
        RestrictionX, // togresx
        RestrictionS, // togress
        RestrictionN  // togresn
    }

    public enum EffectType
    {
        Blank,
        PureTargeting,
        Damage,
        Shield,
        Heal,
        Kill,
        Mana,
        Reroll,
        Revive,
        Dodge,
        Undying,
        Reuse,
        Stun,
        Redirect
    }

    public enum TargetScope
    {
        None,
        SingleAlly,
        AllAllies,
        SingleEnemy,
        AllEnemies,
        Everyone,
        Self
    }

    #endregion

    #region --- BASE FACE CATALOG & RESOLVER ---

    public class BaseFaceDefinition
    {
        public int Id { get; }
        public EffectType Effect { get; }
        public TargetScope Target { get; }
        public bool AllowsPips { get; }
        public bool IsUntargeted => Target is TargetScope.AllAllies or TargetScope.AllEnemies or TargetScope.Everyone or TargetScope.Self;
        public string[] BakedKeywords { get; }

        public BaseFaceDefinition(int id, EffectType effect, TargetScope target, bool allowsPips, params string[] bakedKeywords)
        {
            Id = id;
            Effect = effect;
            Target = target;
            AllowsPips = allowsPips;
            BakedKeywords = bakedKeywords;
        }
    }

    public static class BaseFaceCatalog
    {
        public static readonly List<BaseFaceDefinition> Definitions = new()
        {
            // Blanks
            new(0, EffectType.Blank, TargetScope.None, false),
            new(4, EffectType.Blank, TargetScope.None, false), // Item blank
            new(5, EffectType.Blank, TargetScope.None, false), // Curse blank

            // Pure Targeting (No Effect)
            new(176, EffectType.PureTargeting, TargetScope.SingleAlly, false),
            new(177, EffectType.PureTargeting, TargetScope.SingleAlly, true),
            new(178, EffectType.PureTargeting, TargetScope.AllAllies, true),
            new(179, EffectType.PureTargeting, TargetScope.AllAllies, false),
            new(180, EffectType.PureTargeting, TargetScope.SingleEnemy, false),
            new(181, EffectType.PureTargeting, TargetScope.SingleEnemy, true),
            new(182, EffectType.PureTargeting, TargetScope.AllEnemies, true),
            new(183, EffectType.PureTargeting, TargetScope.AllEnemies, false),
            new(184, EffectType.PureTargeting, TargetScope.Everyone, true),
            new(185, EffectType.PureTargeting, TargetScope.Everyone, false),
            new(186, EffectType.PureTargeting, TargetScope.Self, false),
            new(187, EffectType.PureTargeting, TargetScope.Self, true),

            // Standard Effects (With Pips)
            new(15,  EffectType.Damage, TargetScope.SingleEnemy, true),
            new(56,  EffectType.Shield, TargetScope.SingleAlly, true),
            new(103, EffectType.Heal, TargetScope.SingleAlly, true),
            new(34,  EffectType.Damage, TargetScope.AllEnemies, true),
            new(72,  EffectType.Shield, TargetScope.AllAllies, true),
            new(107, EffectType.Heal, TargetScope.AllAllies, true),
            new(116, EffectType.Kill, TargetScope.SingleEnemy, true),
            new(76,  EffectType.Mana, TargetScope.AllAllies, true),
            new(125, EffectType.Reroll, TargetScope.AllAllies, true),
            new(136, EffectType.Revive, TargetScope.AllAllies, true),

            // Standard Effects (NO Pips)
            new(123, EffectType.Dodge, TargetScope.Self, false),
            new(117, EffectType.Undying, TargetScope.SingleAlly, false),
            new(130, EffectType.Reuse, TargetScope.SingleAlly, false),

            // Self-Damage / Special Base Faces
            new(12,  EffectType.Damage, TargetScope.Self, true, "cantrip"),
            new(14,  EffectType.Damage, TargetScope.Self, true, "mandatory"),

            // Special Effects (Baked Keywords)
            new(128, EffectType.Damage, TargetScope.Everyone, true, "rampage", "pain"),
            new(160, EffectType.Damage, TargetScope.Everyone, true, "charged", "manacost"),
            new(43,  EffectType.Stun, TargetScope.SingleEnemy, false, "bully"),
            new(100, EffectType.Stun, TargetScope.SingleEnemy, false, "singleuse"),
            new(118, EffectType.Redirect, TargetScope.SingleAlly, true, "self-shield")
        };
    }

    public static class BaseFaceResolver
    {
        public static string ResolveFaceString(EffectType effect, TargetScope target, int pips)
        {
            var match = BaseFaceCatalog.Definitions.FirstOrDefault(d =>
                d.Effect == effect &&
                d.Target == target &&
                (pips <= 0 || d.AllowsPips));

            if (match == null)
            {
                match = BaseFaceCatalog.Definitions.FirstOrDefault(d =>
                    d.Effect == EffectType.PureTargeting &&
                    d.Target == target &&
                    (pips <= 0 || d.AllowsPips))
                    ?? BaseFaceCatalog.Definitions.First(d => d.Id == 0);
            }

            if (match.AllowsPips && pips > 0)
            {
                return $"{match.Id}-{pips}";
            }

            return match.Id.ToString();
        }
    }

    #endregion

    #region --- AST & DATA MODELS ---

    public class HatPayload
    {
        public string RawPayload { get; set; } = string.Empty;
        public Face SourceFace { get; set; } = Face.Middle;

        public HatPayload(string rawPayload, Face sourceFace = Face.Middle)
        {
            RawPayload = rawPayload;
            SourceFace = sourceFace;
        }
    }

    public class FaceIntent
    {
        public Face TargetFace { get; set; }
        public SpecialFaceSpec? SpecialFace { get; set; }
        public HatPayload? TargetingSource { get; set; }
        public HatPayload? VisualsSource { get; set; }
        public HatPayload? EffectSource { get; set; }
        public HatPayload? PipsSource { get; set; }
        public HatPayload? KeywordsSource { get; set; }
        public HatPayload? OrEffectSource { get; set; }
        public UntargetedEffectSpec? UntargetedEffect { get; set; }
        public string? Facade { get; set; }
        public RestrictionIntent? LogicGates { get; set; }
        public string? VisualEffectName { get; set; }
        public int PipDelta { get; set; } = 0;
        public List<string> RawKeywords { get; } = new();
        public bool RequiresPipKeywords { get; set; } = false;

        public int BaseEffectId { get; set; } = 0;
        public int BasePips { get; set; } = 0;
        public string Sidesc { get; set; }
        public List<string> AdditionalItems { get; } = new();

        public FaceIntent(Face targetFace)
        {
            TargetFace = targetFace;
        }
        public FaceIntent AddRawKeyword(string keyword, bool requiresPips = false)
        {
            RawKeywords.Add(keyword);
            if (requiresPips) RequiresPipKeywords = true;
            return this;
        }
    }

    public class DieIntent
    {
        public Dictionary<Face, FaceIntent> FaceIntents { get; } = new();
        public Dictionary<Face, string> BaseFaceOverrides { get; } = new();
        public bool FlipTargetAllegiance { get; set; } = false;
        public bool FlipDuration { get; set; } = false;
        public List<string> GlobalItems { get; } = new();

        public FaceIntent GetOrCreateFace(Face face)
        {
            if (!FaceIntents.TryGetValue(face, out var intent))
            {
                intent = new FaceIntent(face);
                FaceIntents[face] = intent;
            }
            return intent;
        }
    }

    public class ItemGroup
    {
        public Face? SidePrefix { get; set; }
        public List<string> Instructions { get; } = new();
        public ItemGroup(Face? sidePrefix = null)
        {
            SidePrefix = sidePrefix;
        }

        public string Compile()
        {
            if (Instructions.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            if (SidePrefix.HasValue && SidePrefix.Value != Face.All)
            {
                sb.Append(SidePrefix.Value.ToString().ToLower()).Append(".");
            }

            sb.Append(string.Join("#", Instructions));
            return sb.ToString();
        }
    }

    public class HatNode
    {
        public string BaseHero { get; set; } = "Fey";
        public string SdDeclaration { get; set; } = string.Empty;
        public List<ItemGroup> ItemGroups { get; } = new();

        public string Compile()
        {
            var sb = new StringBuilder();
            sb.Append(BaseHero);

            if (!string.IsNullOrEmpty(SdDeclaration))
            {
                sb.Append(".sd.").Append(SdDeclaration);
            }

            foreach (var group in ItemGroups)
            {
                string groupPayload = group.Compile();
                if (!string.IsNullOrWhiteSpace(groupPayload))
                {
                    sb.Append(".i.").Append(groupPayload);
                }
            }

            return sb.ToString();
        }
    }

    public static class EggFaceRouter
    {
        // Registers where the Egg face natively exists
        private static readonly HashSet<Face> ValidEggSourceSides = new()
    {
        Face.Left,
        Face.Middle,
        Face.Right
    };

        public static string FormatEggHatInstruction(Face targetFace, string entityPayload, bool keepDeathKeyword)
        {
            string targetSide = targetFace.ToString().ToLower();
            string blindfoldSuffix = keepDeathKeyword ? string.Empty : "#blindfold";

            // If targeting a face that natively has the egg side, target side equals source side
            if (ValidEggSourceSides.Contains(targetFace))
            {
                return $"{targetSide}.hat.(egg.({entityPayload}){blindfoldSuffix})";
            }

            // Target face does NOT natively have an egg side (e.g. Top, Bottom, Rightmost).
            // Map target register to a valid source register (Middle by default).
            string sourceSide = Face.Middle.ToString().ToLower();
            return $"{targetSide}.{sourceSide}.hat.(egg.({entityPayload}){blindfoldSuffix})";
        }
    }
    #endregion

    #region --- VIRTUAL MACHINE & REGISTER TRACKER ---

    public class VirtualDieState
    {
        private readonly Dictionary<Face, bool> _lockedRegisters = new();

        // NEW: Tracks if a face currently possesses pips
        private readonly Dictionary<Face, bool> _hasPips = new();

        public VirtualDieState()
        {
            Reset();
        }

        public void Reset()
        {
            foreach (Face face in Enum.GetValues(typeof(Face)))
            {
                if (face == Face.All) continue;
                _lockedRegisters[face] = false;
                _hasPips[face] = false; // Default pip state
            }
        }

        public bool IsLocked(Face face) => _lockedRegisters.TryGetValue(face, out var isLocked) && isLocked;

        public void Lock(Face face)
        {
            if (face != Face.All) _lockedRegisters[face] = true;
        }

        // NEW METHODS: Pip Tracking
        public bool FaceHasPips(Face face) => _hasPips.TryGetValue(face, out var hasPips) && hasPips;

        public void MarkFaceAsPipped(Face face, bool hasPips = true)
        {
            if (face != Face.All)
            {
                _hasPips[face] = hasPips;
            }
            else
            {
                // If applied to All, update all active registers
                foreach (var key in _hasPips.Keys.ToList())
                {
                    _hasPips[key] = hasPips;
                }
            }
        }

        public Face AllocateScratchpad()
        {
            Face[] preferredScratchpads = { Face.Top, Face.Bottom, Face.Right, Face.Rightmost, Face.Middle };

            foreach (var face in preferredScratchpads)
            {
                if (!_lockedRegisters[face])
                    return face;
            }

            throw new InvalidOperationException("Register Allocation Failure: No free faces available for scratchpad operations.");
        }
    }
    #endregion

    #region --- COMPILER ENGINE ---

    public class SliceAndDiceCompiler
    {
        private readonly List<string> _instructions = new();
        private readonly VirtualDieState _vm = new();

        public string Compile(DieIntent intent)
        {
            _instructions.Clear();
            _vm.Reset();

            string sdHeader = BuildSdHeader(intent);
            Emit(sdHeader);

            if (intent.FlipTargetAllegiance) Emit("togfri");
            if (intent.FlipDuration) Emit("togtime");

            foreach (var gItem in intent.GlobalItems)
            {
                if (!string.IsNullOrWhiteSpace(gItem)) Emit(gItem);
            }

            Face? leftScratchpad = null;
            if (intent.FaceIntents.TryGetValue(Face.Left, out var leftIntent))
            {
                leftScratchpad = _vm.AllocateScratchpad();
                leftIntent.TargetFace = leftScratchpad.Value;
                _vm.Lock(leftScratchpad.Value);
            }

            foreach (var kvp in intent.FaceIntents.Where(x => x.Key != Face.Left))
            {
                CompileFaceIntent(kvp.Value);
            }

            if (leftScratchpad.HasValue && leftIntent != null)
            {
                CompileFaceIntent(leftIntent);
                EmitSwap(Face.Left, leftScratchpad.Value);
            }

            return string.Join(" ", _instructions);
        }
        private string BuildSdHeader(DieIntent intent)
        {
            Face[] executionOrder = { Face.Left, Face.Middle, Face.Top, Face.Bottom, Face.Right, Face.Rightmost };
            var faceStrings = new List<string>();
            foreach (var face in executionOrder)
            {
                if (intent.FaceIntents.TryGetValue(face, out var faceIntent) && (faceIntent.BaseEffectId != 0 || faceIntent.BasePips != 0))
                {
                    if (faceIntent.BasePips == 0) faceStrings.Add(faceIntent.BaseEffectId.ToString());
                    else faceStrings.Add($"{faceIntent.BaseEffectId}-{faceIntent.BasePips}");
                    _vm.MarkFaceAsPipped(face, faceIntent.BasePips > 0);
                }
                else if (intent.BaseFaceOverrides.TryGetValue(face, out var faceStr))
                {
                    faceStrings.Add(faceStr);
                    _vm.MarkFaceAsPipped(face, faceStr.Contains("-"));
                }
                else
                {
                    faceStrings.Add("0");
                    _vm.MarkFaceAsPipped(face, false);
                }
            }
            return $"sd.{string.Join(":", faceStrings)}";
        }
        private void CompileFaceIntent(FaceIntent intent)
        {
            if (intent.SpecialFace != null)
            {
                EmitSpecialFace(intent.TargetFace, intent.SpecialFace);
            }
            if (intent.PipsSource != null)
            {
                EmitHatTog(intent.TargetFace, TogType.Pips, intent.PipsSource);
                _vm.MarkFaceAsPipped(intent.TargetFace);
            }
            EmitPipMath(intent.TargetFace, intent.PipDelta);
            if (intent.KeywordsSource != null)
                EmitHatTog(intent.TargetFace, TogType.Keywords, intent.KeywordsSource);
            if (intent.EffectSource != null)
                EmitHatTog(intent.TargetFace, TogType.Effect, intent.EffectSource);
            if (intent.TargetingSource != null)
                EmitHatTog(intent.TargetFace, TogType.Targeting, intent.TargetingSource);
            if (intent.VisualsSource != null)
                EmitHatTog(intent.TargetFace, TogType.Visuals, intent.VisualsSource);
            if (intent.OrEffectSource != null)
                EmitHatTog(intent.TargetFace, TogType.OrEffect, intent.OrEffectSource);
            if (intent.UntargetedEffect != null)
            {
                EmitUntargetedEffect(intent.TargetFace, intent.UntargetedEffect);
            }
            if (intent.RawKeywords.Any())
            {
                string sidePrefix = intent.TargetFace == Face.All ? "" : $"{intent.TargetFace.ToString().ToLower()}.";
                if (!_vm.FaceHasPips(intent.TargetFace) && intent.RequiresPipKeywords)
                {
                    Emit($"{sidePrefix}k.permissive");
                }
                string joinedKeywords = string.Join("#k.", intent.RawKeywords);
                Emit($"{sidePrefix}k.{joinedKeywords}");
            }
            if (intent.LogicGates != null)
            {
                EmitRestrictions(intent.TargetFace, intent.LogicGates);
            }
            if (!string.IsNullOrWhiteSpace(intent.VisualEffectName))
            {
                EmitVisualEffect(intent.TargetFace, intent.VisualEffectName);
            }
            if (!string.IsNullOrWhiteSpace(intent.Facade) && intent.SpecialFace == null)
            {
                string side = intent.TargetFace.ToString().ToLower();
                Emit($"{side}.facade.{intent.Facade}");
            }

            if (!string.IsNullOrWhiteSpace(intent.Sidesc))
            {
                string side = intent.TargetFace == Face.All ? "" : $"{intent.TargetFace.ToString().ToLower()}.";
                Emit($"{side}sidesc.{intent.Sidesc}");
            }
            foreach (var item in intent.AdditionalItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    string side = intent.TargetFace == Face.All ? "" : $"{intent.TargetFace.ToString().ToLower()}.";
                    Emit($"{side}{item}");
                }
            }

            _vm.Lock(intent.TargetFace);
        }
        public void RestoreLeftFace()
        {
            Emit("Memory");
            _vm.IsLocked(Face.Left); // Ensure left is tracked properly
        }
        private void EmitRestrictions(Face targetFace, RestrictionIntent intent)
        {
            string targetSide = targetFace == Face.All ? "" : $"{targetFace.ToString().ToLower()}.";

            foreach (var step in intent.Steps)
            {
                switch (step.Operation)
                {
                    // --- STATEFUL OPERATIONS (Require loading data onto Left) ---
                    case RestrictionOp.Base:
                    case RestrictionOp.And:
                    case RestrictionOp.Or:
                    case RestrictionOp.Xor:

                        // 1. Load condition onto Left face (e.g., 'left.k.engage')
                        Emit($"left.{step.Payload}");

                        // 2. Execute target's gate logic relative to Left
                        string opCmd = step.Operation switch
                        {
                            RestrictionOp.Base => "togres",
                            RestrictionOp.And => "togresa",
                            RestrictionOp.Or => "togreso",
                            RestrictionOp.Xor => "togresx",
                            _ => throw new NotImplementedException()
                        };
                        Emit($"{targetSide}{opCmd}");

                        // 3. Clear the Left face register so the next step evaluates cleanly
                        RestoreLeftFace(); // Emits 'Memory' and updates tracking
                        break;


                    // --- STATELESS OPERATIONS (Operate only on the target face's current state) ---
                    case RestrictionOp.Not:
                        Emit($"{targetSide}togresn");
                        break;

                    case RestrictionOp.SwapTarget:
                        Emit($"{targetSide}togress");
                        break;

                    case RestrictionOp.Multiplier:
                        Emit($"{targetSide}togresm");
                        break;
                }
            }
        }
        private void EmitSpecialFace(Face targetFace, SpecialFaceSpec spec)
        {
            string targetSide = targetFace.ToString().ToLower();
            string baseInstruction;
            TargetScope nativeScope;

            switch (spec.Type)
            {
                case SpecialFaceType.Sticker:
                    baseInstruction = $"{targetSide}.sticker.({spec.Payload})";
                    nativeScope = TargetScope.SingleAlly; // Stickers natively start as 1 Ally
                    break;

                case SpecialFaceType.Cast:
                    baseInstruction = $"{targetSide}.cast.({spec.Payload})";
                    nativeScope = TargetScope.SingleAlly;
                    break;

                case SpecialFaceType.Enchant:
                    baseInstruction = $"{targetSide}.enchant.({spec.Payload})";
                    nativeScope = TargetScope.AllAllies; // Enchants natively affect the fight scope (Untargeted/All)
                    break;

                case SpecialFaceType.Egg:
                    baseInstruction = EggFaceRouter.FormatEggHatInstruction(
                        targetFace,
                        spec.Payload,
                        spec.KeepDeathKeyword
                    );
                    nativeScope = TargetScope.AllAllies;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(spec.Type), spec.Type, null);
            }

            // 1. Apply Targeting Transformations (wraps in targeting hats recursively if needed)
            string transformedInstruction = TargetingPipeline.ApplyTargetingTransform(
                targetFace,
                baseInstruction,
                nativeScope,
                spec.TargetScope
            );

            // 2. Append Visual Overrides (#facade) at the outer boundary
            if (!string.IsNullOrWhiteSpace(spec.Facade))
            {
                transformedInstruction += $"#facade.{spec.Facade}";
            }

            Emit(transformedInstruction);
        }
        private void EmitHatTog(Face targetFace, TogType togType, HatPayload hat)
        {
            string hatSourceSide = hat.SourceFace.ToString().ToLower();
            Emit($"left.{hatSourceSide}.hat.({hat.RawPayload})");

            string togOp = MapTogType(togType);

            if (targetFace == Face.All)
            {
                Emit(togOp);
            }
            else
            {
                string targetSide = targetFace.ToString().ToLower();
                Emit($"{targetSide}.{togOp}");
            }
        }

        #region --- UNTARGETED COMPOSITE PIPELINE (TOGUNT) ---

        private void EmitUntargetedEffect(Face targetFace, UntargetedEffectSpec spec)
        {
            string targetSide = targetFace == Face.All ? "" : $"{targetFace.ToString().ToLower()}.";
            string sourceSide = spec.SourceEffect.SourceFace.ToString().ToLower();

            // 1. Load source payload onto Left face (Accumulator)
            string leftPayloadInstruction = $"left.{sourceSide}.hat.({spec.SourceEffect.RawPayload})";

            // 2. TARGETING CONVERSION: If the payload is targeted, convert Left face to untargeted first
            if (!spec.IsInherentlyUntargeted)
            {
                // Enforce an untargeted scope (AllEnemies, AllAllies, Everyone, or Self)
                TargetScope conversionScope = spec.UntargetedConversionScope;
                if (conversionScope is not (TargetScope.AllEnemies or TargetScope.AllAllies or TargetScope.Everyone or TargetScope.Self))
                {
                    conversionScope = TargetScope.AllEnemies; // Default fallback
                }

                leftPayloadInstruction = TargetingPipeline.ApplyTargetingTransform(
                    Face.Left,
                    leftPayloadInstruction,
                    TargetScope.SingleEnemy, // Baseline assumption for targeted input
                    conversionScope
                );
            }

            // 3. Emit payload to Left register
            Emit(leftPayloadInstruction);

            // 4. Emit togunt to copy untargeted effect from Left to target face
            Emit($"{targetSide}togunt");

            // 5. Restore Accumulator baseline
            RestoreLeftFace();
        }

        #endregion

        #region --- DUAL-MODE REGISTER BUS ENGINE ---

        public class RegisterCopyMapping
        {
            public string RitemxToken { get; }
            public string? HumanToken { get; }

            public RegisterCopyMapping(string ritemxToken, string? humanToken = null)
            {
                RitemxToken = ritemxToken;
                HumanToken = humanToken;
            }

            public string GetToken(bool preferHuman)
            {
                if (preferHuman && !string.IsNullOrEmpty(HumanToken))
                    return HumanToken;

                return RitemxToken;
            }
        }

        /// <summary>
        /// When true, emits legacy human-readable items (e.g. 'mid.Pendulum', 'top.Compass') 
        /// instead of optimized 'ritemx.*' hashes where available.
        /// </summary>
        public bool PreferHumanReadableTokens { get; set; } = false;

        private static readonly Dictionary<(Face source, Face target), RegisterCopyMapping> RegisterCopyMatrix = new()
        {
            // --- FROM LEFT ---
            { (Face.Left, Face.Middle),    new("ritemx.13e44", "mid.Pendulum") },
            { (Face.Left, Face.Top),       new("ritemx.13ff5", "top.Compass") },
            { (Face.Left, Face.Bottom),    new("ritemx.14726", null) },
            { (Face.Left, Face.Right),     new("ritemx.10fd8", "right.Origami") },
            { (Face.Left, Face.Rightmost), new("ritemx.12009", "rightmost.Ballet Shoes") },

            // --- FROM MIDDLE ---
            { (Face.Middle, Face.Left),      new("ritemx.1193c", "left.Pendulum") },
            { (Face.Middle, Face.Top),       new("ritemx.17894", "top.Dragonhide Gloves") },
            { (Face.Middle, Face.Bottom),    new("ritemx.16a62", "bot.Dragonhide Gloves") },
            { (Face.Middle, Face.Right),     new("ritemx.5dff",  null) },
            { (Face.Middle, Face.Rightmost), new("ritemx.79",    "rightmost.Origami") },

            // --- FROM TOP ---
            { (Face.Top, Face.Left)      , new("ritemx.a6e6",  "ritemx.a6e6") },
            { (Face.Top, Face.Middle)    , new("ritemx.157cc", null) },
            { (Face.Top, Face.Bottom)    , new("ritemx.101",   "bot.Twiddle") },
            { (Face.Top, Face.Right)     , new("ritemx.50",    "right.Compass") },
            { (Face.Top, Face.Rightmost) , new("ritemx.10b3",  "rightmost.Liqueur.part.0") },

            // --- FROM BOTTOM ---
            { (Face.Bottom, Face.Left)      , new("ritemx.11f",   "left.Compass") },
            { (Face.Bottom, Face.Middle)    , new("ritemx.7ec0",  null) },
            { (Face.Bottom, Face.Top)       , new("ritemx.6b4f",  "top.Twiddle") },
            { (Face.Bottom, Face.Right)     , new("ritemx.1862b", null) },
            { (Face.Bottom, Face.Rightmost) , new("ritemx.e2",    null) },

            // --- FROM RIGHT ---
            { (Face.Right, Face.Left)      , new("ritemx.4e",    "left.Origami") },
            { (Face.Right, Face.Middle)    , new("ritemx.dee",   null) },
            { (Face.Right, Face.Top)       , new("ritemx.1a5",   null) },
            { (Face.Right, Face.Bottom)    , new("ritemx.96",    "bot.Compass") },
            { (Face.Right, Face.Rightmost) , new("ritemx.11703", "Kilt") },

            // --- FROM RIGHTMOST ---
            { (Face.Rightmost, Face.Left)   , new("ritemx.1337e", "left.Ballet Shoes") },
            { (Face.Rightmost, Face.Middle) , new("ritemx.89e1",  "mid.Origami") },
            { (Face.Rightmost, Face.Top)    , new("ritemx.d0ac",  "top.Liqueur.part.0") },
            { (Face.Rightmost, Face.Bottom) , new("ritemx.f5a8",  null) },
            { (Face.Rightmost, Face.Right)  , new("ritemx.15d77", null) }
        };

        /// <summary>
        /// Emits a guaranteed 1-pass copy, choosing between human-readable items and ritemx hashes based on compiler config.
        /// </summary>
        private void EmitCopy(Face source, Face target)
        {
            if (source == target) return; // Ignore self-copies

            if (RegisterCopyMatrix.TryGetValue((source, target), out var mapping))
            {
                string token = mapping.GetToken(PreferHumanReadableTokens);
                Emit(token);
            }
            else
            {
                throw new InvalidOperationException($"Unmapped register copy matrix pair: {source} to {target}");
            }
        }
        #endregion

        #region --- REGISTER BUS OPERATIONS (SWAPS & COPIES) ---

        /// <summary>
        /// Emits a two-way swap instruction between two registers.
        /// </summary>
        private void EmitSwap(Face faceA, Face faceB)
        {
            if (faceA == faceB) return;

            // Direct Non-Left Swaps
            if ((faceA == Face.Top && faceB == Face.Bottom) || (faceA == Face.Bottom && faceB == Face.Top))
            {
                Emit("Twiddle");
                return;
            }
            if ((faceA == Face.Top && faceB == Face.Rightmost) || (faceA == Face.Rightmost && faceB == Face.Top))
            {
                Emit("Liqueur.part.0");
                return;
            }

            // Left-Relative Swaps
            Face other = (faceA == Face.Left) ? faceB : (faceB == Face.Left) ? faceA : Face.All;

            if (other != Face.All)
            {
                switch (other)
                {
                    case Face.Middle: Emit("Pendulum"); return;
                    case Face.Rightmost: Emit("Ballet Shoes"); return;
                    case Face.Right: Emit("Origami"); return; // Swaps Left and Right
                    case Face.Bottom: Emit("Compass"); return; // Swaps Left and Bottom
                }
            }

            // Fallback: Swap via Left scratchpad
            EmitSwap(faceA, Face.Left);
            EmitSwap(Face.Left, faceB);
            EmitSwap(faceA, Face.Left);
        }

        #endregion

        private string MapTogType(TogType type)
        {
            return type switch
            {
                TogType.Targeting => "togtarg",
                TogType.Visuals => "togvis",
                TogType.Effect => "togeft",
                TogType.Pips => "togpip",
                TogType.Keywords => "togkey",
                TogType.OrEffect => "togorf",
                TogType.Untargeted => "togunt",
                TogType.Restriction => "togres",
                TogType.RestrictionM => "togresm",
                TogType.RestrictionA => "togresa",
                TogType.RestrictionO => "togreso",
                TogType.RestrictionX => "togresx",
                TogType.RestrictionS => "togress",
                TogType.RestrictionN => "togresn",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
        private void Emit(string instruction)
        {
            _instructions.Add(instruction);
        }
        private void EmitVisualEffect(Face targetFace, string visualName)
        {
            // 1. Resolve human name from catalog
            if (!VisualEffectsData.VisualEffects.TryGetValue(visualName, out var visualSource))
            {
                throw new KeyNotFoundException($"Visual Effect Compiler Error: '{visualName}' not found in VisualEffectsData catalog.");
            }

            string targetSide = targetFace == Face.All ? "" : $"{targetFace.ToString().ToLower()}.";

            // 2. OPTIMIZATION: If target IS Left, load directly onto Left without togvis
            if (targetFace == Face.Left)
            {
                if (visualSource.StartsWith("sd.", StringComparison.OrdinalIgnoreCase))
                {
                    Emit($"left.mid.hat.(Fey.{visualSource})");
                }
                else
                {
                    Emit(visualSource);
                }
                return;
            }

            // 3. TARGET IS NOT LEFT: Load onto Left (AX), broadcast via togvis, then clean up Left
            if (visualSource.StartsWith("sd.", StringComparison.OrdinalIgnoreCase))
            {
                // Standard SD visual (e.g. "sd.15"): Load via anonymous Fey hat onto Left
                Emit($"left.mid.hat.(Fey.{visualSource})");
            }
            else
            {
                // Cast or Monster Hat visual (e.g. "left.cast.drop", "left.hat.bee")
                Emit(visualSource);
            }

            // Push visual from Left to Target
            Emit($"{targetSide}togvis");

            // Clear Accumulator
            RestoreLeftFace();
        }

        #region --- PIP ALGEBRA ---

        /// <summary>
        /// Compiles pip addition or subtraction into exact Eye of Horus syntax constraints.
        /// Max repeat is 9, max multiplier is 9 or -9.
        /// </summary>
        private void EmitPipMath(Face target, int delta)
        {
            if (delta == 0) return;

            string targetSide = target == Face.All ? "" : $"{target.ToString().ToLower()}.";
            string baseItem = $"{targetSide}Eye of Horus";

            int remaining = delta;

            while (remaining != 0)
            {
                if (remaining > 0)
                {
                    int chunk = Math.Min(remaining, 9);
                    if (chunk == 1) Emit(baseItem);
                    else Emit($"{targetSide}x{chunk}.Eye of Horus"); // x2 through x9
                    remaining -= chunk;
                }
                else // Negative delta
                {
                    int chunk = Math.Max(remaining, -9);
                    Emit($"{baseItem}.m.{chunk}"); // e.g. Eye of Horus.m.-2
                    remaining -= chunk;
                }
            }
        }

        #endregion

    }

    #endregion

    #region --- INTERNAL HAT ASSEMBLY ENGINE ---

    public class HatBuilder
    {
        private readonly HatNode _hat = new();
        private ItemGroup _currentGroup;

        public HatBuilder(string baseHero = "Fey")
        {
            _hat.BaseHero = baseHero;
            _currentGroup = new ItemGroup();
        }
        public HatBuilder SetSd(params string[] faces)
        {
            var paddedFaces = faces.ToList();
            while (paddedFaces.Count < 6) paddedFaces.Add("0");

            _hat.SdDeclaration = string.Join(":", paddedFaces.Take(6));
            return this;
        }
        public HatBuilder NextItem(Face? sidePrefix = null)
        {
            if (_currentGroup.Instructions.Count > 0)
            {
                _hat.ItemGroups.Add(_currentGroup);
            }
            _currentGroup = new ItemGroup(sidePrefix);
            return this;
        }
        public HatBuilder AddRawItem(string itemName)
        {
            _currentGroup.Instructions.Add(itemName);
            return this;
        }
        public HatBuilder AddKeyword(string keyword)
        {
            _currentGroup.Instructions.Add($"k.{keyword}");
            return this;
        }
        public HatPayload Build(Face targetFaceToExtract = Face.Middle)
        {
            if (_currentGroup.Instructions.Count > 0)
            {
                _hat.ItemGroups.Add(_currentGroup);
            }

            return new HatPayload(_hat.Compile(), targetFaceToExtract);
        }
    }

    #endregion

    #region --- RESTRICTIONS & LOGIC GATES ---

    public enum RestrictionOp
    {
        Base,       // togres   - Sets initial restriction
        And,        // togresa  - ANDs with Left
        Or,         // togreso  - ORs with Left
        Xor,        // togresx  - XORs with Left
        Not,        // togresn  - Inverts current state
        SwapTarget, // togress  - Swaps I/Target
        Multiplier  // togresm  - Converts to x2 conditional
    }

    public class RestrictionStep
    {
        public RestrictionOp Operation { get; }

        /// <summary>
        /// The payload loaded onto Left (e.g., "k.pristine", "cast.scald"). 
        /// Null for stateless operators like Not, Swap, Multiplier.
        /// </summary>
        public string? Payload { get; }

        public RestrictionStep(RestrictionOp operation, string? payload = null)
        {
            Operation = operation;
            Payload = payload;
        }
    }

    /// <summary>
    /// Represents a sequential chain of boolean logic evaluated on a face.
    /// </summary>
    public class RestrictionIntent
    {
        public List<RestrictionStep> Steps { get; } = new();
        public RestrictionIntent StartWith(string payload)
        {
            Steps.Add(new RestrictionStep(RestrictionOp.Base, payload));
            return this;
        }
        public RestrictionIntent And(string payload)
        {
            Steps.Add(new RestrictionStep(RestrictionOp.And, payload));
            return this;
        }
        public RestrictionIntent Or(string payload)
        {
            Steps.Add(new RestrictionStep(RestrictionOp.Or, payload));
            return this;
        }
        public RestrictionIntent Xor(string payload)
        {
            Steps.Add(new RestrictionStep(RestrictionOp.Xor, payload));
            return this;
        }
        public RestrictionIntent Not()
        {
            Steps.Add(new RestrictionStep(RestrictionOp.Not));
            return this;
        }
        public RestrictionIntent SwapTarget()
        {
            Steps.Add(new RestrictionStep(RestrictionOp.SwapTarget));
            return this;
        }
        public RestrictionIntent AsMultiplier()
        {
            Steps.Add(new RestrictionStep(RestrictionOp.Multiplier));
            return this;
        }
    }
    #endregion
}

public static class TargetingPipeline
{
    /// <summary>
    /// Dynamically transforms an inner face instruction to a desired TargetScope by querying 
    /// the BaseFaceCatalog and programmatically assembling an optimized Hat AST.
    /// </summary>
    public static string ApplyTargetingTransform(
        Face targetRegister,
        string innerInstruction,
        TargetScope currentScope,
        TargetScope desiredScope)
    {
        // 1. NO-OP
        if (currentScope == desiredScope || desiredScope == TargetScope.None)
        {
            return innerInstruction;
        }

        string side = targetRegister.ToString().ToLower();

        // 2. DIRECT ALLEGIANCE FLIP (SingleAlly <-> SingleEnemy)
        if (currentScope == TargetScope.SingleAlly && desiredScope == TargetScope.SingleEnemy)
        {
            return $"{innerInstruction}#togfri";
        }
        if (currentScope == TargetScope.SingleEnemy && desiredScope == TargetScope.SingleAlly)
        {
            return $"{innerInstruction}#togfri";
        }

        // 3. DYNAMIC CATALOG QUERY
        // Query catalog for a PureTargeting base face definition matching the desired scope
        bool needsAllegianceFlip = false;
        TargetScope queryScope = desiredScope;

        // Special handling for AllEnemies: if no direct pure-targeting face without pips exists,
        // query AllAllies and set allegiance flip flag
        if (desiredScope == TargetScope.AllEnemies)
        {
            queryScope = TargetScope.AllAllies;
            needsAllegianceFlip = true;
        }

        BaseFaceDefinition? pureTargetDef = BaseFaceCatalog.Definitions.FirstOrDefault(d =>
            d.Effect == EffectType.PureTargeting &&
            d.Target == queryScope &&
            !d.AllowsPips);

        if (pureTargetDef == null)
        {
            // Fallback: any pure targeting face matching scope
            pureTargetDef = BaseFaceCatalog.Definitions.FirstOrDefault(d =>
                d.Effect == EffectType.PureTargeting &&
                d.Target == queryScope)
                ?? throw new InvalidOperationException($"Catalog Error: No pure targeting base face found for scope {desiredScope}");
        }

        // 4. PROGRAMMATIC HAT AST GENERATION
        // Set Left face (register 0) of the Hat to the queried pure targeting base ID
        var hatBuilder = new HatBuilder("Fey")
            .SetSd(pureTargetDef.Id.ToString()); // Left = pure targeting ID, rest blank

        // Inside the Hat frame: target face executes inner instruction, then propagates targeting from Left via togtarg
        string cleanInstruction = StripSidePrefix(innerInstruction, side);

        hatBuilder.NextItem(targetRegister)
                  .AddRawItem(cleanInstruction)
                  .AddRawItem("togtarg");

        if (needsAllegianceFlip)
        {
            hatBuilder.AddRawItem("togfri");
        }

        HatPayload compiledHat = hatBuilder.Build(targetFaceToExtract: targetRegister);

        // 5. EMISSION BOUNDARY
        return $"{side}.hat.({compiledHat.RawPayload})";
    }

    /// <summary>
    /// Strips redundant side prefixes when nesting instructions inside a side-scoped Hat group.
    /// </summary>
    private static string StripSidePrefix(string instruction, string side)
    {
        string prefix = $"{side}.";
        if (instruction.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return instruction.Substring(prefix.Length);
        }
        return instruction;
    }
}

public static class CompositePipeline
{
    public static string FormatOrEffect(Face targetFace, HatPayload effectSource, bool requiresPips)
    {
        string side = targetFace.ToString().ToLower();

        if (!requiresPips)
        {
            // Simple Route: Destroys pips on the OR effect, fine for pipless ORs
            string sourceSide = effectSource.SourceFace.ToString().ToLower();
            return $"{side}.hat.(Fey.sd.0.i.left.{sourceSide}.hat.({effectSource.RawPayload})#togorf)";
        }
        else
        {
            // Complex Route: Avoids -999 pip destruction bug.
            // Uses Middle face as the execution container, applies togtarg/Compass/Pendulum/togunt mechanics 
            // exactly as defined in the provided workaround architecture.
            string sourceSide = effectSource.SourceFace.ToString().ToLower();
            return $"{side}.hat.(Fey.sd.0.i.((left.{sourceSide}.hat.({effectSource.RawPayload}).i.((mid.togtarg)#Compass#Pendulum#togunt)))#togorf)";
        }
    }
}
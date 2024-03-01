﻿using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.FCWorkshops;
using Artisan.GameInterop;
using Artisan.GameInterop.CSExt;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static ECommons.GenericHelpers;

namespace Artisan
{
    internal class RecipeWindowUI : Window
    {
        private static string search = string.Empty;
        private static bool searched = false;
        internal static string Search
        {
            get => search;
            set
            {
                if (search != value)
                {
                    search = value;
                    searched = false;
                }
            }
        }
        public RecipeWindowUI() : base($"###RecipeWindow", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoNavInputs)
        {
            this.Size = new Vector2(0, 0);
            this.Position = new Vector2(0, 0);
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            this.SizeConstraints = new WindowSizeConstraints()
            {
                MaximumSize = new Vector2(0, 0),
            };
        }

        public override void Draw()
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;

            if (!P.Config.DisableMiniMenu)
            {
                if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting] || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.PreparingToCraft])
                    DrawOptions();
            }

            DrawSearchReplace();

            DrawEnduranceCounter();

            DrawWorkshopOverlay();

            DrawSupplyMissionOverlay();

            DrawMacroOptions();
        }

        private unsafe void DrawSearchReplace()
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (!addon->AtkUnitBase.IsVisible)
                {
                    Search = "";
                    return;
                }
                var searchNode = addon->AtkUnitBase.GetNodeById(26);
                var searchLabel = addon->AtkUnitBase.GetNodeById(25);
                if (searchNode == null || searchLabel == null) return;

                if (P.Config.ReplaceSearch)
                {
                    //searchNode->ToggleVisibility(false);
                    searchLabel->GetAsAtkTextNode()->SetText("Artisan Search");
                }
                else
                {
                    //searchNode->ToggleVisibility(true);
                    string searchText = Svc.Data.Excel.GetSheet<Addon>().GetRow(1412).Text;
                    searchLabel->GetAsAtkTextNode()->SetText(searchText);
                    return;
                }

                var textInput = (AtkComponentTextInput*)searchNode->GetComponent();
                Search = Marshal.PtrToStringAnsi(new IntPtr(textInput->AtkComponentInputBase.UnkText1.StringPtr)).Trim();
                var textSize = ImGui.CalcTextSize(Search);

                var position = AtkResNodeFunctions.GetNodePosition(searchNode);
                var scale = AtkResNodeFunctions.GetNodeScale(searchNode);
                var size = new Vector2(searchNode->Width, searchNode->Height) * scale;
                var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y + size.Y));

                //ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5f, 2.5f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f, 3f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                ImGui.GetFont().Scale = scale.X;
                var oldScale = ImGui.GetIO().FontGlobalScale;
                ImGui.GetIO().FontGlobalScale = 1f;
                ImGui.PushFont(ImGui.GetFont());

                var compNode = (AtkComponentNode*)searchNode;
                if (compNode->Component->UldManager.SearchNodeById(18) == null) return;

                searched = !compNode->Component->UldManager.SearchNodeById(18)->IsVisible;
                try
                {
                    if (Search.Length > 0 && !searched)
                    {
                        if (LuminaSheets.RecipeSheet.Values.Count(x => Regex.Match(x.ItemResult.Value.Name.RawString, Search, RegexOptions.IgnoreCase).Success) > 0)
                        {
                            ImGui.Begin($"###Search{searchNode->NodeID}", ImGuiWindowFlags.NoScrollbar
                                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

                            ImGui.AlignTextToFramePadding();
                            ImGui.SetNextItemWidth(size.Length() - 12f);

                            int results = 0;
                            foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => Regex.Match(x.ItemResult.Value.Name.RawString, Search, RegexOptions.IgnoreCase).Success))
                            {
                                if (results >= 24) continue;
                                var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name} ({(Job)recipe.CraftType.Row + 8})###{recipe.RowId}");
                                if (selected)
                                {
                                    var orid = Operations.GetSelectedRecipeEntry();
                                    if (orid == null || (orid != null && orid->RecipeId != recipe.RowId))
                                    {
                                        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
                                    }

                                    searched = true;
                                }
                                results++;
                            }
                            ImGui.End();
                        }
                    }
                }
                catch(Exception ex)
                {
                    if (ex is not RegexParseException)
                        ex.Log();
                }


                ImGui.GetFont().Scale = 1;
                ImGui.GetIO().FontGlobalScale = oldScale;
                ImGui.PopFont();
                ImGui.PopStyleVar(5);
                //ImGui.PopStyleColor();
            }
        }

        private unsafe void DrawSupplyMissionOverlay()
        {
            if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon))
            {
                try
                {
                    var subcontext = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu");
                    if (subcontext != null && subcontext->IsVisible)
                        return;

                    if (addon->SupplyRadioButton is null)
                        return;

                    if (addon->SupplyRadioButton->AtkComponentBase.UldManager.NodeList[1] != null && addon->SupplyRadioButton->AtkComponentBase.UldManager.NodeList[1]->IsVisible)
                        return;

                    var timerWindow = Svc.GameGui.GetAddonByName("GrandCompanySupplyList");
                    if (timerWindow == IntPtr.Zero)
                        return;

                    var atkUnitBase = (AtkUnitBase*)timerWindow;
                    var node = atkUnitBase->UldManager.NodeList[19];

                    if (!node->IsVisible)
                        return;

                    var position = AtkResNodeFunctions.GetNodePosition(node);
                    var scale = AtkResNodeFunctions.GetNodeScale(node);
                    var size = new Vector2(node->Width, node->Height) * scale;
                    var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

                    var oldScale = ImGui.GetIO().FontGlobalScale;
                    ImGui.GetIO().FontGlobalScale = 1f * scale.X;

                    var textSize = ImGui.CalcTextSize("Create Crafting List");

                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y + (textSize.Y * scale.Y) + (14f * scale.Y)));

                    ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0f, 2f * scale.Y));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f * scale.X, 3f * scale.Y));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

                    ImGui.Begin($"###SupplyTimerWindow", ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings
                        | ImGuiWindowFlags.AlwaysAutoResize);

                    if (ImGui.Button($"Create Crafting List", new Vector2(size.X / 2, 0)))
                    {
                        CreateGCListAgent(atkUnitBase, false);
                        P.PluginUi.IsOpen = true;
                        P.PluginUi.OpenWindow = OpenWindow.Lists;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Create Crafting List (with subcrafts)", new Vector2(size.X / 2, 0)))
                    {
                        CreateGCListAgent(atkUnitBase, true);
                        P.PluginUi.IsOpen = true;
                        P.PluginUi.OpenWindow = OpenWindow.Lists;
                    }

                    ImGui.End();
                    ImGui.PopStyleVar(5);
                    ImGui.GetIO().FontGlobalScale = oldScale;
                    ImGui.PopFont();
                    ImGui.PopStyleColor();


                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }
            else
            {
                try
                {
                    var subcontext = (AtkUnitBase*)Svc.GameGui.GetAddonByName("AddonContextSub");

                    if (subcontext != null && subcontext->IsVisible)
                        return;

                    subcontext = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu");
                    if (subcontext != null && subcontext->IsVisible)
                        return;

                    var timerWindow = Svc.GameGui.GetAddonByName("ContentsInfoDetail");
                    if (timerWindow == IntPtr.Zero)
                        return;

                    var atkUnitBase = (AtkUnitBase*)timerWindow;

                    if (atkUnitBase->AtkValues[233].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int)
                        return;

                    var node = atkUnitBase->UldManager.NodeList[97];

                    if (!node->IsVisible)
                        return;

                    var position = AtkResNodeFunctions.GetNodePosition(node);
                    var scale = AtkResNodeFunctions.GetNodeScale(node);
                    var size = new Vector2(node->Width, node->Height) * scale;
                    var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

                    var oldScale = ImGui.GetIO().FontGlobalScale;
                    ImGui.GetIO().FontGlobalScale = 1f * scale.X;

                    var textSize = ImGui.CalcTextSize("Create Crafting List");

                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y - (textSize.Y * scale.Y) - (5f * scale.Y)));

                    ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0f, 2f * scale.Y));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f * scale.X, 3f * scale.Y));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

                    ImGui.Begin($"###SupplyTimerWindow", ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings
                        | ImGuiWindowFlags.AlwaysAutoResize);

                    if (ImGui.Button($"Create Crafting List", new Vector2(size.X / 2, 0)))
                    {
                        CreateGCList(atkUnitBase, false);
                        P.PluginUi.IsOpen = true;
                        P.PluginUi.OpenWindow = OpenWindow.Lists;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Create Crafting List (with subcrafts)", new Vector2(size.X / 2, 0)))
                    {
                        CreateGCList(atkUnitBase, true);
                        P.PluginUi.IsOpen = true;
                        P.PluginUi.OpenWindow = OpenWindow.Lists;
                    }

                    ImGui.End();
                    ImGui.PopStyleVar(5);
                    ImGui.GetIO().FontGlobalScale = oldScale;
                    ImGui.PopFont();
                    ImGui.PopStyleColor();


                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }
        }

        private static unsafe void CreateGCListAgent(AtkUnitBase* atkUnitBase, bool withSubcrafts)
        {
            CraftingList craftingList = new CraftingList();
            craftingList.Name = $"GC Supply List ({DateTime.Now.ToShortDateString()})";

            for (int i = 425; i <= 432; i++)
            {
                if (atkUnitBase->AtkValues[i].Type == 0)
                    continue;

                var itemId = atkUnitBase->AtkValues[i].Int;
                var requested = atkUnitBase->AtkValues[i - 40].Int;
                uint job = TextureIdToJob(atkUnitBase->AtkValues[i - 360].Int);

                if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId && x.CraftType.Row + 8 == job, out var recipe))
                {
                    var timesToAdd = requested / recipe.AmountResult;

                    if (withSubcrafts)
                        CraftingListUI.AddAllSubcrafts(recipe, craftingList, timesToAdd);

                    for (int p = 1; p <= timesToAdd; p++)
                    {
                        if (craftingList.Items.IndexOf(recipe.RowId) == -1)
                        {
                            craftingList.Items.Add(recipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = craftingList.Items.IndexOf(recipe.RowId);
                            craftingList.Items.Insert(indexOfLast, recipe.RowId);
                        }
                    }

                }
            }

            craftingList.SetID();
            craftingList.Save(true);

            Notify.Success("Crafting List Created");
        }

        private static uint TextureIdToJob(int textureId)
        {
            return textureId switch
            {
                62008 => 8,
                62009 => 9,
                62010 => 10,
                62011 => 11,
                62012 => 12,
                62013 => 13,
                62014 => 14,
                62015 => 15,
                _ => 0
            };
        }

        private static unsafe void CreateGCList(AtkUnitBase* atkUnitBase, bool withSubcrafts)
        {
            CraftingList craftingList = new CraftingList();
            craftingList.Name = $"GC Supply List ({DateTime.Now.ToShortDateString()})";

            for (int i = 233; i <= 240; i++)
            {
                if (atkUnitBase->AtkValues[i].Type == 0)
                    continue;

                var itemId = atkUnitBase->AtkValues[i].Int;
                var requested = atkUnitBase->AtkValues[i + 16].Int;
                uint job = TextureIdToJob(atkUnitBase->AtkValues[i + 8].Int);

                if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId && x.CraftType.Row + 8 == job, out var recipe))
                {
                    var timesToAdd = requested / recipe.AmountResult;

                    if (withSubcrafts)
                        CraftingListUI.AddAllSubcrafts(recipe, craftingList, timesToAdd);

                    for (int p = 1; p <= timesToAdd; p++)
                    {
                        if (craftingList.Items.IndexOf(recipe.RowId) == -1)
                        {
                            craftingList.Items.Add(recipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = craftingList.Items.IndexOf(recipe.RowId);
                            craftingList.Items.Insert(indexOfLast, recipe.RowId);
                        }
                    }

                }
            }

            craftingList.SetID();
            craftingList.Save(true);

            Notify.Success("Crafting List Created");
        }

        private unsafe void DrawWorkshopOverlay()
        {
            try
            {
                var subWindow = Svc.GameGui.GetAddonByName("SubmarinePartsMenu", 1);
                if (subWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AtkUnitBase*)subWindow;
                if (addonPtr == null)
                    return;

                if (addonPtr->UldManager.NodeListCount < 38)
                    return;

                var node = addonPtr->UldManager.NodeList[2];

                if (!node->IsVisible)
                    return;

                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var size = new Vector2(node->Width, node->Height) * scale;
                var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
                var textSize = ImGui.CalcTextSize("Create crafting list for this phase");

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + (4f * scale.X), position.Y + size.Y - textSize.Y - (34f * scale.Y)));

                ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                float oldSize = ImGui.GetFont().Scale;
                ImGui.GetFont().Scale *= scale.X;
                ImGui.PushFont(ImGui.GetFont());
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 5f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f, 3f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                ImGui.Begin($"###WorkshopButton{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                    | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);


                if (ImGui.Button("Create crafting list for this phase"))
                {
                    var itemNameNode = addonPtr->UldManager.NodeList[37]->GetAsAtkTextNode();
                    var phaseProgress = addonPtr->UldManager.NodeList[26]->GetAsAtkTextNode();

                    if (LuminaSheets.WorkshopSequenceSheet.Values.Any(x => x.ResultItem.Value.Name.ExtractText() == itemNameNode->NodeText.ExtractText()))
                    {
                        var project = LuminaSheets.WorkshopSequenceSheet.Values.First(x => x.ResultItem.Value.Name.ExtractText() == itemNameNode->NodeText.ExtractText());
                        var phaseNum = Convert.ToInt32(phaseProgress->NodeText.ToString().First().ToString());

                        if (project.CompanyCraftPart.Count(x => x.Row > 0) == 1)
                        {
                            var part = project.CompanyCraftPart.First(x => x.Row > 0).Value;
                            var phase = part.CompanyCraftProcess[phaseNum - 1];

                            FCWorkshopUI.CreatePhaseList(phase.Value!, part.CompanyCraftType.Value.Name.ExtractText(), phaseNum, false, null, project);
                            Notify.Success("FC Workshop List Created");
                        }
                        else
                        {
                            var currentPartNode = addonPtr->UldManager.NodeList[28]->GetAsAtkTextNode();
                            string partStep = currentPartNode->NodeText.ExtractText().Split(":").Last();

                            if (project.CompanyCraftPart.Any(x => x.Value.CompanyCraftType.Value.Name.ExtractText() == partStep))
                            {
                                var part = project.CompanyCraftPart.First(x => x.Value.CompanyCraftType.Value.Name.ExtractText() == partStep).Value;
                                var phase = part.CompanyCraftProcess[phaseNum - 1];

                                FCWorkshopUI.CreatePhaseList(phase.Value!, part.CompanyCraftType.Value.Name.ExtractText(), phaseNum, false, null, project);
                                Notify.Success("FC Workshop List Created");
                            }
                        }
                    }
                }

                if (ImGui.Button("Create crafting list for this phase (including precrafts)"))
                {
                    var itemNameNode = addonPtr->UldManager.NodeList[37]->GetAsAtkTextNode();
                    var phaseProgress = addonPtr->UldManager.NodeList[26]->GetAsAtkTextNode();

                    if (LuminaSheets.WorkshopSequenceSheet.Values.Any(x => x.ResultItem.Value.Name.ExtractText() == itemNameNode->NodeText.ExtractText()))
                    {
                        var project = LuminaSheets.WorkshopSequenceSheet.Values.First(x => x.ResultItem.Value.Name.ExtractText() == itemNameNode->NodeText.ExtractText());
                        var phaseNum = Convert.ToInt32(phaseProgress->NodeText.ToString().First().ToString());

                        if (project.CompanyCraftPart.Count(x => x.Row > 0) == 1)
                        {
                            var part = project.CompanyCraftPart.First(x => x.Row > 0).Value;
                            var phase = part.CompanyCraftProcess[phaseNum - 1];

                            FCWorkshopUI.CreatePhaseList(phase.Value!, part.CompanyCraftType.Value.Name.ExtractText(), phaseNum, true, null, project);
                            Notify.Success("FC Workshop List Created");
                        }
                        else
                        {
                            var currentPartNode = addonPtr->UldManager.NodeList[28]->GetAsAtkTextNode();
                            string partStep = currentPartNode->NodeText.ExtractText().Split(":").Last();

                            if (project.CompanyCraftPart.Any(x => x.Value.CompanyCraftType.Value.Name.ExtractText() == partStep))
                            {
                                var part = project.CompanyCraftPart.First(x => x.Value.CompanyCraftType.Value.Name.ExtractText() == partStep).Value;
                                var phase = part.CompanyCraftProcess[phaseNum - 1];

                                FCWorkshopUI.CreatePhaseList(phase.Value!, part.CompanyCraftType.Value.Name.ExtractText(), phaseNum, true, null, project);
                                Notify.Success("FC Workshop List Created");
                            }
                        }
                    }
                }

                ImGui.End();
                ImGui.PopStyleVar(5);
                ImGui.GetFont().Scale = oldSize;
                ImGui.PopFont();
                ImGui.PopStyleColor();

            }
            catch { }
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }


        public unsafe static void DrawOptions()
        {
            var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (recipeWindow == IntPtr.Zero)
                return;

            var addonPtr = (AtkUnitBase*)recipeWindow;
            if (addonPtr == null)
                return;

            var baseX = addonPtr->X;
            var baseY = addonPtr->Y;

            if (addonPtr->UldManager.NodeListCount > 1)
            {
                if (addonPtr->UldManager.NodeList[1]->IsVisible)
                {
                    var node = addonPtr->UldManager.NodeList[1];

                    if (!node->IsVisible)
                        return;

                    if (P.Config.LockMiniMenu)
                    {
                        var position = AtkResNodeFunctions.GetNodePosition(node);
                        var scale = AtkResNodeFunctions.GetNodeScale(node);
                        var size = new Vector2(node->Width, node->Height) * scale;
                        var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
                        //position += ImGuiHelpers.MainViewport.Pos;

                        ImGuiHelpers.ForceNextWindowMainViewport();

                        if ((AtkResNodeFunctions.ResetPosition && position.X != 0) || P.Config.LockMiniMenu)
                        {
                            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.X + 7, position.Y + 7), ImGuiCond.Always);
                            AtkResNodeFunctions.ResetPosition = false;
                        }
                        else
                        {
                            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.X + 7, position.Y + 7), ImGuiCond.FirstUseEver);
                        }
                    }

                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7f, 7f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                    var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.AlwaysUseWindowPadding;
                    if (P.Config.PinMiniMenu)
                        flags |= ImGuiWindowFlags.NoMove;

                    ImGui.Begin($"###Options{node->NodeID}", flags);


                    DrawCopyOfCraftMenu();

                    ImGui.End();
                    ImGui.PopStyleVar(2);
                }
            }

        }

        private static void DrawCopyOfCraftMenu()
        {
            if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
            {
                P.PluginUi.IsOpen = true;
            }

            bool autoMode = P.Config.AutoMode;

            if (ImGui.Checkbox("Automatic Action Execution Mode", ref autoMode))
            {
                P.Config.AutoMode = autoMode;
                P.Config.Save();
            }
            bool enable = Endurance.Enable;

            if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID))
                ImGui.BeginDisabled();

            if (ImGui.Checkbox("Endurance Mode Toggle", ref enable))
            {
                Endurance.ToggleEndurance(enable);
            }

            if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID))
            {
                ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"You cannot start Endurance as you do not possess ingredients to craft this recipe.");
                    ImGui.EndTooltip();
                }
            }
        }

        public unsafe static void DrawMacroOptions()
        {
            var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (recipeWindow == IntPtr.Zero)
                return;

            var addonPtr = (AtkUnitBase*)recipeWindow;
            if (addonPtr == null)
                return;

            var baseX = addonPtr->X;
            var baseY = addonPtr->Y;

            if (addonPtr->UldManager.NodeListCount >= 2 && addonPtr->UldManager.NodeList[1]->IsVisible)
            {
                var node = addonPtr->UldManager.NodeList[1];

                if (!node->IsVisible)
                    return;

                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var size = new Vector2(node->Width, node->Height) * scale;
                var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

                ImGuiHelpers.ForceNextWindowMainViewport();
                if ((AtkResNodeFunctions.ResetPosition && position.X != 0) || P.Config.LockMiniMenu)
                {
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.X + 7, position.Y + 7), ImGuiCond.FirstUseEver);
                    AtkResNodeFunctions.ResetPosition = false;
                }
                else
                {
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.X + 7, position.Y + 7), ImGuiCond.FirstUseEver);
                }

                //Svc.Log.Debug($"{position.X + node->Width + 7}");
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7f, 7f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                ImGui.Begin($"###Options{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.AlwaysUseWindowPadding);

                ImGui.Spacing();

                if (SimpleTweaks.IsFocusTweakEnabled())
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, $@"Warning: You have the ""Auto Focus Recipe Search"" SimpleTweak enabled. This is highly incompatible with Artisan and is recommended to disable it.");
                }
                if (Endurance.RecipeID != 0)
                {
                    var recipe = LuminaSheets.RecipeSheet[Endurance.RecipeID];
                    ImGuiEx.ImGuiLineCentered("###RecipeWindowRecipeName", () => { ImGuiEx.TextUnderlined($"{recipe.ItemResult.Value.Name}"); });
                    var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId) ?? new();
                    var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.Row);
                    stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ));
                    var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.Row, recipe);
                    if (config.Draw(craft))
                    {
                        Svc.Log.Debug($"Updating config for {recipe.RowId}");
                        P.Config.RecipeConfigs[recipe.RowId] = config;
                        P.Config.Save();
                    }

                    if (!P.Config.HideRecipeWindowSimulator)
                    {
                        var solver = CraftingProcessor.GetSolverForRecipe(config, craft).CreateSolver(craft);
                        var rd = RecipeNoteRecipeData.Ptr();
                        var re = rd != null ? rd->FindRecipeById(recipe.RowId) : null;
                        var startingQuality = re != null ? Calculations.GetStartingQuality(recipe, re->GetAssignedHQIngredients()) : 0;
                        var time = SolverUtils.EstimateCraftTime(solver, craft, startingQuality);
                        var result = SolverUtils.SimulateSolverExecution(solver, craft, startingQuality);
                        var status = result != null ? Simulator.Status(craft, result) : Simulator.CraftStatus.InProgress;
                        var hq = result != null ? Calculations.GetHQChance((float)result.Quality / craft.CraftQualityMax * 100) : 0;

                        string solverHint = status switch
                        {
                            Simulator.CraftStatus.InProgress => "Craft did not finish (solver failed to return any more steps before finishing).",
                            Simulator.CraftStatus.FailedDurability => $"Craft failed due to durability shortage. (P: {(float)result.Progress / craft.CraftProgress * 100:f0}%, Q: {(float)result.Quality / craft.CraftQualityMax * 100:f0}%)",
                            Simulator.CraftStatus.FailedMinQuality => "Craft completed but didn't meet minimum quality.",
                            Simulator.CraftStatus.SucceededQ1 => $"Craft completed and managed to hit 1st quality threshold in {time.TotalSeconds:f0}s.",
                            Simulator.CraftStatus.SucceededQ2 => $"Craft completed and managed to hit 2nd quality threshold in {time.TotalSeconds:f0}s.",
                            Simulator.CraftStatus.SucceededQ3 => $"Craft completed and managed to hit 3rd quality threshold in {time.TotalSeconds:f0}s!",
                            Simulator.CraftStatus.SucceededMaxQuality => $"Craft completed with full quality in {time.TotalSeconds:f0}s!",
                            Simulator.CraftStatus.SucceededSomeQuality => $"Craft completed but didn't max out quality ({hq}%) in {time.TotalSeconds:f0}s",
                            Simulator.CraftStatus.SucceededNoQualityReq => $"Craft completed, no quality required in {time.TotalSeconds:f0}s!",
                            Simulator.CraftStatus.Count => "You shouldn't be able to see this. Report it please.",
                            _ => "You shouldn't be able to see this. Report it please.",
                        };


                        Vector4 hintColor = status switch
                        {
                            Simulator.CraftStatus.InProgress => ImGuiColors.DalamudWhite,
                            Simulator.CraftStatus.FailedDurability => ImGuiColors.DalamudRed,
                            Simulator.CraftStatus.FailedMinQuality => ImGuiColors.DalamudRed,
                            Simulator.CraftStatus.SucceededQ1 => new Vector4(0.7f, 0.5f, 0.5f, 1f),
                            Simulator.CraftStatus.SucceededQ2 => new Vector4(0.5f, 0.5f, 0.7f, 1f),
                            Simulator.CraftStatus.SucceededQ3 => new Vector4(0.5f, 1f, 0.5f, 1f),
                            Simulator.CraftStatus.SucceededMaxQuality => ImGuiColors.ParsedGreen,
                            Simulator.CraftStatus.SucceededSomeQuality => new Vector4(1 - (hq / 100f), 0 + (hq / 100f), 1 - (hq / 100f), 255),
                            Simulator.CraftStatus.SucceededNoQualityReq => ImGuiColors.ParsedGreen,
                            Simulator.CraftStatus.Count => ImGuiColors.DalamudWhite,
                            _ => ImGuiColors.DalamudWhite,
                        };

                        if (!recipe.IsExpert)
                            ImGuiEx.TextWrapped(hintColor, solverHint);
                        else
                            ImGuiEx.TextWrapped($"Please run this recipe in the simulator for results.");

                        if (ImGui.IsItemClicked())
                        {
                            P.PluginUi.OpenWindow = UI.OpenWindow.Simulator;
                            P.PluginUi.IsOpen = true;
                            SimulatorUI.SelectedRecipe = recipe;
                            SimulatorUI.ResetSim();
                            if (config.RequiredPotion > 0)
                            {
                                SimulatorUI.SimMedicine ??= new();
                                SimulatorUI.SimMedicine.Id = config.RequiredPotion;
                                SimulatorUI.SimMedicine.ConsumableHQ = config.RequiredPotionHQ;
                                SimulatorUI.SimMedicine.Stats = new ConsumableStats(config.RequiredPotion, config.RequiredPotionHQ);
                            }
                            if (config.RequiredFood > 0)
                            {
                                SimulatorUI.SimFood ??= new();
                                SimulatorUI.SimFood.Id = config.RequiredFood;
                                SimulatorUI.SimFood.ConsumableHQ = config.RequiredFoodHQ;
                                SimulatorUI.SimFood.Stats = new ConsumableStats(config.RequiredFood, config.RequiredFoodHQ);
                            }

                            foreach (ref var gs in RaptureGearsetModule.Instance()->EntriesSpan)
                            {
                                if ((Job)gs.ClassJob == Job.CRP + recipe.CraftType.Row)
                                {
                                    if (SimulatorUI.SimGS is null || (Job)SimulatorUI.SimGS.Value.ClassJob != Job.CRP + recipe.CraftType.Row)
                                    {
                                        SimulatorUI.SimGS = gs;
                                    }

                                    if (SimulatorUI.SimGS.Value.ItemLevel < gs.ItemLevel)
                                        SimulatorUI.SimGS = gs;
                                }
                            }

                            var rawSolver = CraftingProcessor.GetSolverForRecipe(config, craft);
                            SimulatorUI._selectedSolver = new(rawSolver.Name, rawSolver.Def.Create(craft, rawSolver.Flavour));
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGuiEx.Tooltip($"Click to open in simulator");
                        }


                    }

                }

                ImGui.End();
                ImGui.PopStyleVar(2);
            }
        }

        internal static unsafe void DrawEnduranceCounter()
        {
            if (Endurance.RecipeID == 0)
                return;

            var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (recipeWindow == IntPtr.Zero)
                return;

            var addonPtr = (AtkUnitBase*)recipeWindow;
            if (addonPtr == null)
                return;

            var baseX = addonPtr->X;
            var baseY = addonPtr->Y;

            if (addonPtr->UldManager.NodeListCount >= 5)
            {
                //var node = addonPtr->UldManager.NodeList[1]->GetAsAtkComponentNode()->Component->UldManager.NodeList[4];
                var node = addonPtr->UldManager.NodeList[8];

                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var size = new Vector2(node->Width, node->Height) * scale;
                var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
                //position += ImGuiHelpers.MainViewport.Pos;
                var textHeight = ImGui.CalcTextSize("Craft X Times:");
                var craftableCount = addonPtr->UldManager.NodeList[35]->GetAsAtkTextNode()->NodeText.ToString() == "" ? 0 : Convert.ToInt32(addonPtr->UldManager.NodeList[35]->GetAsAtkTextNode()->NodeText.ToString().GetNumbers());

                if (craftableCount == 0) return;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + (4f * scale.X) - 40f, position.Y - 16f - (17f * scale.Y)));

                //Svc.Log.Debug($"Length: {size.Length()}, Width: {node->Width}, Scale: {scale.Y}");

                ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5f, 2.5f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f, 3f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                ImGui.GetFont().Scale = scale.X;
                var oldScale = ImGui.GetIO().FontGlobalScale;
                ImGui.GetIO().FontGlobalScale = 1f;
                ImGui.PushFont(ImGui.GetFont());

                ImGui.Begin($"###Repeat{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                    | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Craft X Times:");
                ImGui.SameLine();
                ImGui.PushItemWidth(110f * scale.X);
                if (ImGui.InputInt($"###TimesRepeat{node->NodeID}", ref P.Config.CraftX))
                {
                    if (P.Config.CraftX < 0)
                        P.Config.CraftX = 0;

                    if (P.Config.CraftX > craftableCount)
                        P.Config.CraftX = craftableCount;

                }
                ImGui.SameLine();
                if (P.Config.CraftX > 0)
                {
                    if (ImGui.Button($"Craft {P.Config.CraftX}"))
                    {
                        P.Config.CraftingX = true;
                        Endurance.ToggleEndurance(true);
                    }
                }
                else
                {
                    if (ImGui.Button($"Craft All ({craftableCount})"))
                    {
                        P.Config.CraftX = craftableCount;
                        P.Config.CraftingX = true;
                        Endurance.ToggleEndurance(true);
                    }
                }

                ImGui.End();

                ImGui.GetFont().Scale = 1;
                ImGui.GetIO().FontGlobalScale = oldScale;
                ImGui.PopFont();
                ImGui.PopStyleVar(5);
                ImGui.PopStyleColor();
            }
        }
    }
}

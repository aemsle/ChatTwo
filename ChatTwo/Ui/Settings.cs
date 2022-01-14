﻿using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Command;
using Dalamud.Interface;
using ImGuiNET;

namespace ChatTwo.Ui;

internal sealed class Settings : IUiComponent {
    private PluginUi Ui { get; }

    private bool _hideChat;
    private bool _nativeItemTooltips;
    private bool _sidebarTabView;
    private float _fontSize;
    private Dictionary<ChatType, uint> _chatColours = new();
    private List<Tab> _tabs = new();

    internal Settings(PluginUi ui) {
        this.Ui = ui;
        this.Ui.Plugin.CommandManager.AddHandler("/chat2", new CommandInfo(this.Command) {
            HelpMessage = "Toggle the Chat 2 settings",
        });
    }

    public void Dispose() {
        this.Ui.Plugin.CommandManager.RemoveHandler("/chat2");
    }

    private void Command(string command, string args) {
        this.Ui.SettingsVisible ^= true;
    }

    private void Initialise() {
        var config = this.Ui.Plugin.Config;
        this._hideChat = config.HideChat;
        this._nativeItemTooltips = config.NativeItemTooltips;
        this._sidebarTabView = config.SidebarTabView;
        this._fontSize = config.FontSize;
        this._chatColours = config.ChatColours.ToDictionary(entry => entry.Key, entry => entry.Value);
        this._tabs = config.Tabs.Select(tab => tab.Clone()).ToList();
    }

    public void Draw() {
        if (!this.Ui.SettingsVisible) {
            return;
        }

        if (!ImGui.Begin($"{this.Ui.Plugin.Name} settings", ref this.Ui.SettingsVisible)) {
            ImGui.End();
            return;
        }

        if (ImGui.IsWindowAppearing()) {
            this.Initialise();
        }

        var height = ImGui.GetContentRegionAvail().Y
                     - ImGui.GetStyle().FramePadding.Y * 2
                     - ImGui.GetStyle().ItemSpacing.Y
                     - ImGui.GetStyle().ItemInnerSpacing.Y * 2
                     - ImGui.CalcTextSize("A").Y;
        if (ImGui.BeginChild("##chat2-settings", new Vector2(-1, height))) {
            ImGui.Checkbox("Hide chat", ref this._hideChat);
            ImGui.Checkbox("Show native item tooltips", ref this._nativeItemTooltips);
            ImGui.Checkbox("Show tabs in a sidebar", ref this._sidebarTabView);
            ImGui.DragFloat("Font size", ref this._fontSize, .5f, 12f, 36f);

            if (ImGui.TreeNodeEx("Chat colours")) {
                foreach (var type in Enum.GetValues<ChatType>()) {
                    if (ImGui.Button($"Default##{type}")) {
                        this._chatColours.Remove(type);
                    }

                    ImGui.SameLine();

                    var vec = this._chatColours.TryGetValue(type, out var colour)
                        ? ColourUtil.RgbaToVector3(colour)
                        : ColourUtil.RgbaToVector3(type.DefaultColour() ?? 0);
                    if (ImGui.ColorEdit3(type.Name(), ref vec, ImGuiColorEditFlags.NoInputs)) {
                        this._chatColours[type] = ColourUtil.Vector3ToRgba(vec);
                    }
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx("Tabs")) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, tooltip: "Add")) {
                    this._tabs.Add(new Tab());
                }

                var toRemove = -1;
                for (var i = 0; i < this._tabs.Count; i++) {
                    var tab = this._tabs[i];

                    if (ImGui.TreeNodeEx($"{tab.Name}###tab-{i}")) {
                        ImGui.PushID($"tab-{i}");

                        if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: "Delete")) {
                            toRemove = i;
                        }

                        ImGui.SameLine();

                        if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, tooltip: "Move up") && i > 0) {
                            (this._tabs[i - 1], this._tabs[i]) = (this._tabs[i], this._tabs[i - 1]);
                        }

                        ImGui.SameLine();

                        if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, tooltip: "Move down") && i < this._tabs.Count - 1) {
                            (this._tabs[i + 1], this._tabs[i]) = (this._tabs[i], this._tabs[i + 1]);
                        }

                        ImGui.InputText("Name", ref tab.Name, 512, ImGuiInputTextFlags.EnterReturnsTrue);
                        ImGui.Checkbox("Show unread count", ref tab.DisplayUnread);
                        ImGui.Checkbox("Show timestamps", ref tab.DisplayTimestamp);

                        var input = tab.Channel?.ToChatType().Name() ?? "<None>";
                        if (ImGui.BeginCombo("Input channel", input)) {
                            if (ImGui.Selectable("<None>", tab.Channel == null)) {
                                tab.Channel = null;
                            }

                            foreach (var channel in Enum.GetValues<InputChannel>()) {
                                if (ImGui.Selectable(channel.ToChatType().Name(), tab.Channel == channel)) {
                                    tab.Channel = channel;
                                }
                            }

                            ImGui.EndCombo();
                        }

                        if (ImGui.TreeNodeEx("Channels")) {
                            foreach (var type in Enum.GetValues<ChatType>()) {
                                var enabled = tab.ChatCodes.ContainsKey(type);
                                if (ImGui.Checkbox($"##{type.Name()}-{i}", ref enabled)) {
                                    if (enabled) {
                                        tab.ChatCodes[type] = ChatSourceExt.All;
                                    } else {
                                        tab.ChatCodes.Remove(type);
                                    }
                                }

                                ImGui.SameLine();

                                if (ImGui.TreeNodeEx($"{type.Name()}##{i}")) {
                                    tab.ChatCodes.TryGetValue(type, out var sourcesEnum);
                                    var sources = (uint) sourcesEnum;

                                    foreach (var source in Enum.GetValues<ChatSource>()) {
                                        if (ImGui.CheckboxFlags(source.ToString(), ref sources, (uint) source)) {
                                            tab.ChatCodes[type] = (ChatSource) sources;
                                        }
                                    }

                                    ImGui.TreePop();
                                }
                            }


                            ImGui.TreePop();
                        }

                        ImGui.TreePop();

                        ImGui.PopID();
                    }
                }

                if (toRemove > -1) {
                    this._tabs.RemoveAt(toRemove);
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        var save = ImGui.Button("Save");

        ImGui.SameLine();

        if (ImGui.Button("Save and close")) {
            save = true;
            this.Ui.SettingsVisible = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard")) {
            this.Ui.SettingsVisible = false;
        }

        ImGui.End();

        if (save) {
            var config = this.Ui.Plugin.Config;

            var hideChatChanged = this._hideChat != this.Ui.Plugin.Config.HideChat;
            var fontSizeChanged = Math.Abs(this._fontSize - this.Ui.Plugin.Config.FontSize) > float.Epsilon;

            config.HideChat = this._hideChat;
            config.NativeItemTooltips = this._nativeItemTooltips;
            config.SidebarTabView = this._sidebarTabView;
            config.FontSize = this._fontSize;
            config.ChatColours = this._chatColours;
            config.Tabs = this._tabs;

            this.Ui.Plugin.SaveConfig();

            this.Ui.Plugin.Store.FilterAllTabs(false);

            if (fontSizeChanged) {
                this.Ui.Plugin.Interface.UiBuilder.RebuildFonts();
            }

            if (!this._hideChat && hideChatChanged) {
                GameFunctions.GameFunctions.SetChatInteractable(true);
            }

            this.Initialise();
        }
    }
}

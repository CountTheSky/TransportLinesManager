using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using Klyte.Commons.UI.SpriteNames;
using Klyte.Commons.Utils;
using Klyte.TransportLinesManager.Extensions;
using Klyte.TransportLinesManager.Overrides;
using Klyte.TransportLinesManager.Utils;
using Klyte.TransportLinesManager.Xml;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Klyte.TransportLinesManager.UI
{
    public class UVMBudgetTimeChart : MonoBehaviour
    {
        private UIPanel m_container;
        private UIRadialChartExtended m_clock;
        private UISprite m_hourPointer;
        private UISprite m_minutePointer;
        private UILabel m_effectiveLabel;
        private UIProgressBar m_effectiveSprite;

        private UIButton m_copyButton;
        private UIButton m_pasteButton;
        private UIButton m_eraseButton;

        private string m_clipboard;


        private void AwakeActionButtons()
        {
            m_copyButton = ConfigureActionButton(m_container, CommonsSpriteNames.K45_Copy);
            m_copyButton.eventClick += (x, y) => ActionCopy();
            m_pasteButton = ConfigureActionButton(m_container, CommonsSpriteNames.K45_Paste);
            m_pasteButton.eventClick += (x, y) => ActionPaste();
            m_pasteButton.isVisible = false;
            m_eraseButton = ConfigureActionButton(m_container, CommonsSpriteNames.K45_RemoveIcon);
            m_eraseButton.eventClick += (x, y) => ActionDelete();
            m_eraseButton.color = Color.red;

            m_copyButton.tooltip =  Locale.Get("K45_TLM_COPY_CURRENT_LIST_CLIPBOARD");
            m_pasteButton.tooltip = Locale.Get("K45_TLM_PASTE_CLIPBOARD_TO_CURRENT_LIST");
            m_eraseButton.tooltip = Locale.Get("K45_TLM_DELETE_CURRENT_LIST");

            m_copyButton.relativePosition = new Vector3(-50, 0);
            m_pasteButton.relativePosition = new Vector3(-50, 25);
            m_eraseButton.relativePosition = new Vector3(m_container.width + 30, 0);
        }

        private void ActionCopy()
        {
            ushort lineID = UVMPublicTransportWorldInfoPanel.GetLineID();
            m_clipboard = XmlUtils.DefaultXmlSerialize(TLMLineUtils.GetEffectiveExtensionForLine(lineID).GetBudgetsMultiplierForLine(lineID));
            m_pasteButton.isVisible = true;
            UVMBudgetConfigTab.Instance.RebuildList(lineID);
        }
        private void ActionPaste()
        {
            if (m_clipboard == null)
            {
                return;
            }
            ushort lineID = UVMPublicTransportWorldInfoPanel.GetLineID();
            TLMLineUtils.GetEffectiveExtensionForLine(lineID).SetAllBudgetMultipliersForLine(lineID, XmlUtils.DefaultXmlDeserialize<TimeableList<BudgetEntryXml>>(m_clipboard));
            UVMBudgetConfigTab.Instance.RebuildList(lineID);
        }
        private void ActionDelete()
        {
            ushort lineID = UVMPublicTransportWorldInfoPanel.GetLineID();
            TLMLineUtils.GetEffectiveExtensionForLine(lineID).RemoveAllBudgetMultipliersOfLine(lineID);
            UVMBudgetConfigTab.Instance.RebuildList(lineID);
        }
        protected static UIButton ConfigureActionButton(UIComponent parent, CommonsSpriteNames spriteName)
        {
            KlyteMonoUtils.CreateUIElement(out UIButton actionButton, parent.transform, "Btn");
            KlyteMonoUtils.InitButton(actionButton, false, "ButtonMenu");
            actionButton.focusedBgSprite = "";
            actionButton.autoSize = false;
            actionButton.width = 20;
            actionButton.height = 20;
            actionButton.foregroundSpriteMode = UIForegroundSpriteMode.Scale;
            actionButton.normalFgSprite = KlyteResourceLoader.GetDefaultSpriteNameFor(spriteName);
            return actionButton;
        }
        public void SetValues(List<Tuple<int, Color, uint>> steps)
        {
            if (steps.Count == 0)
            {
                return;
            }

            steps.Sort((x, y) => x.First - y.First);
            if (steps[0].First != 0)
            {
                steps.Insert(0, Tuple.New(0, steps.Last().Second, steps.Last().Third));
            }
            if (steps.Count != m_clock.sliceCount)
            {
                while (m_clock.sliceCount > 0)
                {
                    m_clock.RemoveSlice(0);
                }
                foreach (Tuple<int, Color, uint> loc in steps)
                {
                    m_clock.AddSlice(loc.Second, loc.Second, 1);// loc.Third / dividerMultiplier);
                }
            }
            else
            {
                for (int i = 0; i < m_clock.sliceCount; i++)
                {
                    m_clock.GetSlice(i).innerColor = steps[i].Second;
                    m_clock.GetSlice(i).outterColor = steps[i].Second;
                    m_clock.GetSlice(i).sizeMultiplier = 1;// steps[i].Third / dividerMultiplier;
                }
            }

            var targetValues = steps.Select(x => Mathf.Round(x.First * 100f / 24f)).ToList();
            m_clock.SetValuesStarts(targetValues.ToArray());
        }

        public void Awake()
        {
            LogUtils.DoLog("AWAKE UVMBudgetTimeChart!");
            UIPanel panel = transform.gameObject.AddComponent<UIPanel>();
            panel.width = 370;
            panel.height = 70;
            panel.autoLayout = false;
            panel.useCenter = true;
            panel.wrapLayout = false;
            panel.tooltipLocaleID = "K45_TLM_BUDGET_CLOCK";

            KlyteMonoUtils.CreateUIElement(out m_container, transform, "ClockContainer");
            m_container.relativePosition = new Vector3((panel.width / 2f) - 70, 0);
            m_container.width = 140;
            m_container.height = 70;
            m_container.autoLayout = false;
            m_container.useCenter = true;
            m_container.wrapLayout = false;
            m_container.tooltipLocaleID = "K45_TLM_BUDGET_CLOCK";

            KlyteMonoUtils.CreateUIElement(out m_clock, m_container.transform, "Clock");
            m_clock.spriteName = "K45_24hClock";
            m_clock.relativePosition = new Vector3(0, 0);
            m_clock.width = 70;
            m_clock.height = 70;

            KlyteMonoUtils.CreateUIElement(out m_minutePointer, m_container.transform, "Minute");
            m_minutePointer.width = 2;
            m_minutePointer.height = 27;
            m_minutePointer.pivot = UIPivotPoint.TopCenter;
            m_minutePointer.relativePosition = new Vector3(35, 35);
            m_minutePointer.spriteName = "EmptySprite";
            m_minutePointer.color = new Color32(35, 35, 35, 255);

            KlyteMonoUtils.CreateUIElement(out m_hourPointer, m_container.transform, "Hour");
            m_hourPointer.width = 3;
            m_hourPointer.height = 14;
            m_hourPointer.pivot = UIPivotPoint.TopCenter;
            m_hourPointer.relativePosition = new Vector3(35, 35);
            m_hourPointer.spriteName = "EmptySprite";
            m_hourPointer.color = new Color32(5, 5, 5, 255);

            KlyteMonoUtils.CreateUIElement(out UILabel titleEffective, m_container.transform, "TitleEffective");
            titleEffective.width = 70;
            titleEffective.height = 30;
            KlyteMonoUtils.LimitWidthAndBox(titleEffective, 70, true);
            titleEffective.relativePosition = new Vector3(70, 0);
            titleEffective.textScale = 0.8f;
            titleEffective.color = Color.white;
            titleEffective.isLocalized = true;
            titleEffective.localeID = "K45_TLM_EFFECTIVE_BUDGET_NOW";
            titleEffective.textAlignment = UIHorizontalAlignment.Center;

            KlyteMonoUtils.CreateUIElement(out UIPanel effectiveContainer, m_container.transform, "ValueEffectiveContainer");
            effectiveContainer.width = 70;
            effectiveContainer.height = 40;
            effectiveContainer.relativePosition = new Vector3(70, 30);
            effectiveContainer.color = Color.white;
            effectiveContainer.autoLayout = false;

            KlyteMonoUtils.CreateUIElement(out m_effectiveSprite, effectiveContainer.transform, "BarBg");
            m_effectiveSprite.width = 70;
            m_effectiveSprite.height = 40;
            m_effectiveSprite.relativePosition = new Vector3(0, 0);
            m_effectiveSprite.backgroundSprite = "PlainWhite";
            m_effectiveSprite.progressSprite = "PlainWhite";
            m_effectiveSprite.color = Color.cyan;
            m_effectiveSprite.progressColor = Color.red;
            m_effectiveSprite.value = 0.5f;

            KlyteMonoUtils.CreateUIElement(out m_effectiveLabel, effectiveContainer.transform, "BarLabel");
            m_effectiveLabel.width = 70;
            m_effectiveLabel.height = 40;
            m_effectiveLabel.relativePosition = new Vector3(0, 0);
            m_effectiveLabel.color = Color.white;
            m_effectiveLabel.isLocalized = false;
            m_effectiveLabel.text = "%\n";
            m_effectiveLabel.textAlignment = UIHorizontalAlignment.Center;
            m_effectiveLabel.verticalAlignment = UIVerticalAlignment.Middle;
            m_effectiveLabel.useOutline = true;
            m_effectiveLabel.padding.top = 3;
            KlyteMonoUtils.LimitWidthAndBox(m_effectiveLabel, 70, true);

            AwakeActionButtons();
        }

        public void Update()
        {
            if (m_container.isVisible)
            {
                ushort lineID = UVMPublicTransportWorldInfoPanel.GetLineID();
                m_minutePointer.transform.localEulerAngles = new Vector3(0, 0, (SimulationManager.instance.m_currentDayTimeHour % 1 * -360) + 180);
                m_hourPointer.transform.localEulerAngles = new Vector3(0, 0, (SimulationManager.instance.m_currentDayTimeHour / 24 * -360) + 180);
                Tuple<float, int, int, float> value = TLMLineUtils.GetBudgetMultiplierLineWithIndexes(lineID);
                m_effectiveSprite.color = UVMBudgetConfigTab.m_colorOrder[value.Second % UVMBudgetConfigTab.m_colorOrder.Count];
                m_effectiveSprite.progressColor = UVMBudgetConfigTab.m_colorOrder[value.Third % UVMBudgetConfigTab.m_colorOrder.Count];
                m_effectiveSprite.value = value.Fourth;
                int currentVehicleCount = Singleton<TransportManager>.instance.m_lines.m_buffer[lineID].CountVehicles(lineID);
                int targetVehicleCount = TransportLineOverrides.NewCalculateTargetVehicleCount(lineID);
                m_effectiveLabel.prefix = (value.First * 100).ToString("0");
                m_effectiveLabel.suffix = $"{currentVehicleCount.ToString("0")}/{targetVehicleCount.ToString("0")}";
            }
        }
    }

}


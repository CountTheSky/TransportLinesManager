﻿using ColossalFramework;
using ColossalFramework.UI;
using Klyte.Commons.Redirectors;
using Klyte.Commons.Utils;
using Klyte.TransportLinesManager.Extensors;
using Klyte.TransportLinesManager.Interfaces;
using Klyte.TransportLinesManager.Xml;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CIdx = Klyte.TransportLinesManager.TLMConfigWarehouse.ConfigIndex;
using TLMCW = Klyte.TransportLinesManager.TLMConfigWarehouse;

namespace Klyte.TransportLinesManager.Utils
{
    public class TLMLineUtils
    {
        public static Vehicle GetVehicleCapacityAndFill(ushort vehicleID, Vehicle vehicleData, out int fill, out int cap)
        {
            ushort firstVehicle = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].GetFirstVehicle(vehicleID);
            vehicleData.Info.m_vehicleAI.GetBufferStatus(firstVehicle, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[firstVehicle], out string text, out fill, out cap);
            return vehicleData;
        }

        public static void GetQuantityPassengerWaiting(ushort currentStop, out int residents, out int tourists, out int timeTilBored)
        {
            ushort nextStop = TransportLine.GetNextStop(currentStop);
            CitizenManager cm = Singleton<CitizenManager>.instance;
            NetManager nm = Singleton<NetManager>.instance;
            Vector3 position = nm.m_nodes.m_buffer[currentStop].m_position;
            Vector3 position2 = nm.m_nodes.m_buffer[nextStop].m_position;
            nm.m_nodes.m_buffer[currentStop].m_maxWaitTime = 0;
            int minX = Mathf.Max((int)((position.x - 32f) / 8f + 1080f), 0);
            int minZ = Mathf.Max((int)((position.z - 32f) / 8f + 1080f), 0);
            int maxX = Mathf.Min((int)((position.x + 32f) / 8f + 1080f), 2159);
            int maxZ = Mathf.Min((int)((position.z + 32f) / 8f + 1080f), 2159);
            residents = 0;
            tourists = 0;
            timeTilBored = 255;
            int zIterator = minZ;
            while (zIterator <= maxZ)
            {
                int xIterator = minX;
                while (xIterator <= maxX)
                {
                    ushort citizenIterator = cm.m_citizenGrid[zIterator * 2160 + xIterator];
                    int loopCounter = 0;
                    while (citizenIterator != 0)
                    {
                        ushort nextGridInstance = cm.m_instances.m_buffer[citizenIterator].m_nextGridInstance;
                        if ((cm.m_instances.m_buffer[citizenIterator].m_flags & CitizenInstance.Flags.WaitingTransport) != CitizenInstance.Flags.None)
                        {
                            Vector3 a = cm.m_instances.m_buffer[citizenIterator].m_targetPos;
                            float distance = Vector3.SqrMagnitude(a - position);
                            if (distance < 1024f)
                            {
                                CitizenInfo info = cm.m_instances.m_buffer[citizenIterator].Info;
                                if (info.m_citizenAI.TransportArriveAtSource(citizenIterator, ref cm.m_instances.m_buffer[citizenIterator], position, position2))
                                {
                                    if ((cm.m_citizens.m_buffer[citizenIterator].m_flags & Citizen.Flags.Tourist) != Citizen.Flags.None)
                                    {
                                        tourists++;
                                    }
                                    else
                                    {
                                        residents++;
                                    }
                                    timeTilBored = Math.Min(255 - cm.m_instances.m_buffer[citizenIterator].m_waitCounter, timeTilBored);
                                }
                            }
                        }
                        citizenIterator = nextGridInstance;
                        if (++loopCounter > 65536)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                    xIterator++;
                }
                zIterator++;
            }
        }

        public static void RemoveAllFromLine(ushort lineId)
        {
            TransportLine line = Singleton<TransportManager>.instance.m_lines.m_buffer[lineId];
            VehicleManager instance = Singleton<VehicleManager>.instance;
            ushort num2 = line.m_vehicles;
            int num3 = 0;
            while (num2 != 0)
            {
                ushort nextLineVehicle = instance.m_vehicles.m_buffer[num2].m_nextLineVehicle;
                VehicleInfo info2 = instance.m_vehicles.m_buffer[num2].Info;
                info2.m_vehicleAI.SetTransportLine(num2, ref instance.m_vehicles.m_buffer[num2], 0);
                num2 = nextLineVehicle;
                if (++num3 > 16384)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
        }



        public static float GetEffectiveBudget(ushort transportLine) => GetEffectiveBudgetInt(transportLine) / 100f;
        public static int GetEffectiveBudgetInt(ushort transportLine)
        {
            TransportInfo info = Singleton<TransportManager>.instance.m_lines.m_buffer[transportLine].Info;
            Tuple<float, int, int, float, bool> lineBudget = GetBudgetMultiplierLineWithIndexes(transportLine);
            int budgetClass = lineBudget.Fifth ? 100 : Singleton<EconomyManager>.instance.GetBudget(info.m_class);
            return (int)(budgetClass * lineBudget.First);
        }

        public static float GetBudgetMultiplierLine(ushort lineId) => GetBudgetMultiplierLineWithIndexes(lineId).First;

        //public static bool GetConfigForLine(ushort lineId, out TransportLineConfiguration lineConfig, out PrefixConfiguration prefixConfig)
        //{
        //    lineConfig = TLMTransportLineExtension.Instance.SafeGet(lineId);
        //    var tsd = TransportSystemDefinition.From(lineId);
        //    prefixConfig = (tsd.GetTransportExtension() as ISafeGettable<PrefixConfiguration>).SafeGet(hasPrefix(ref tsd) ? getPrefix(lineId) : 0);
        //    return lineConfig != null || prefixConfig != null;
        //}

        public static IBasicExtensionStorage GetEffectiveConfigForLine(ushort lineId)
        {
            if (TLMTransportLineExtension.Instance.IsUsingCustomConfig(lineId))
            {
                return TLMTransportLineExtension.Instance.SafeGet(lineId);
            }
            else
            {
                var tsd = TransportSystemDefinition.From(lineId);
                return (tsd.GetTransportExtension() as ISafeGettable<TLMPrefixConfiguration>).SafeGet(hasPrefix(ref tsd) ? getPrefix(lineId) : 0);
            }
        }
        public static IBasicExtension GetEffectiveExtensionForLine(ushort lineId)
        {
            if (TLMTransportLineExtension.Instance.IsUsingCustomConfig(lineId))
            {
                return TLMTransportLineExtension.Instance;
            }
            else
            {
                var tsd = TransportSystemDefinition.From(lineId);
                return tsd.GetTransportExtension();
            }
        }

        public static Tuple<float, int, int, float, bool> GetBudgetMultiplierLineWithIndexes(ushort lineId)
        {
            IBasicExtensionStorage currentConfig = GetEffectiveConfigForLine(lineId);
            TimeableList<BudgetEntryXml> budgetConfig = currentConfig.BudgetEntries;
            if (budgetConfig.Count == 0)
            {
                GetEffectiveExtensionForLine(lineId).SetBudgetMultiplierForLine(lineId, Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].m_budget, 0);
                currentConfig = GetEffectiveConfigForLine(lineId);
                budgetConfig = currentConfig.BudgetEntries;
            }
            Tuple<Tuple<BudgetEntryXml, int>, Tuple<BudgetEntryXml, int>, float> currentBudget = budgetConfig.GetAtHour(Singleton<SimulationManager>.instance.m_currentDayTimeHour);
            return Tuple.New(Mathf.Lerp(currentBudget.First.First.Value, currentBudget.Second.First.Value, currentBudget.Third) / 100f, currentBudget.First.Second, currentBudget.Second.Second, currentBudget.Third, currentConfig is TLMTransportLineConfiguration);

        }
        public static string getLineStringId(ushort lineIdx)
        {
            getLineNamingParameters(lineIdx, out ModoNomenclatura prefix, out Separador s, out ModoNomenclatura suffix, out ModoNomenclatura nonPrefix, out bool zeros, out bool invertPrefixSuffix);
            return TLMUtils.GetString(prefix, s, suffix, nonPrefix, Singleton<TransportManager>.instance.m_lines.m_buffer[lineIdx].m_lineNumber, zeros, invertPrefixSuffix);
        }

        public static void getLineNamingParameters(ushort lineIdx, out ModoNomenclatura prefix, out Separador s, out ModoNomenclatura suffix, out ModoNomenclatura nonPrefix, out bool zeros, out bool invertPrefixSuffix) => getLineNamingParameters(lineIdx, out prefix, out s, out suffix, out nonPrefix, out zeros, out invertPrefixSuffix, out string nil);
        public static int GetStopsCount(ushort lineID) => Singleton<TransportManager>.instance.m_lines.m_buffer[lineID].CountStops(lineID);

        public static int GetVehiclesCount(ushort lineID) => Singleton<TransportManager>.instance.m_lines.m_buffer[lineID].CountVehicles(lineID);

        public static float GetLineLength(ushort lineID)
        {
            //float totalSize = 0f;
            //for (int i = 0; i < Singleton<TransportManager>.instance.m_lineCurves[(int) lineID].Length; i++) {
            //    Bezier3 bez = Singleton<TransportManager>.instance.m_lineCurves[(int) lineID][i];
            //    totalSize += TLMUtils.calcBezierLenght(bez.a, bez.b, bez.c, bez.d, 0.1f);
            //}
            return Singleton<TransportManager>.instance.m_lines.m_buffer[lineID].m_totalLength;
        }

        public static TransportSystemDefinition getLineNamingParameters(ushort lineIdx, out ModoNomenclatura prefix, out Separador s, out ModoNomenclatura suffix, out ModoNomenclatura nonPrefix, out bool zeros, out bool invertPrefixSuffix, out string icon)
        {
            var tsd = TransportSystemDefinition.GetDefinitionForLine(lineIdx);
            if (tsd != default)
            {
                GetNamingRulesFromTSD(out prefix, out s, out suffix, out nonPrefix, out zeros, out invertPrefixSuffix, ref tsd);
            }
            else
            {
                suffix = default;
                s = default;
                prefix = default;
                nonPrefix = default;
                zeros = false;
                invertPrefixSuffix = false;
            }
            icon = getIconForLine(lineIdx);
            return tsd;
        }

        public static bool IsLineNumberAlredyInUse(int numLinha, ref TransportSystemDefinition tsdOr, int exclude)
        {
            numLinha = numLinha & 0xFFFF;
            if (numLinha == 0)
            {
                return true;
            }

            TLMUtils.doLog("tsdOr = " + tsdOr + " | lineNum =" + numLinha + "| cfgIdx = " + tsdOr.ToConfigIndex());
            var tipo = tsdOr.ToConfigIndex();

            for (ushort i = 1; i < Singleton<TransportManager>.instance.m_lines.m_buffer.Length; i++)
            {
                if ((Singleton<TransportManager>.instance.m_lines.m_buffer[i].m_flags & TransportLine.Flags.Complete) == TransportLine.Flags.None)
                {
                    continue;
                }
                ushort lnum = Singleton<TransportManager>.instance.m_lines.m_buffer[i].m_lineNumber;
                var tsd = TransportSystemDefinition.GetDefinitionForLine(i);
                TLMUtils.doLog("tsd = " + tsd + "| lineNum = " + lnum + "| I=" + i + "| cfgIdx = " + tsd.ToConfigIndex());
                if (tsd != default && i != exclude && tsd.ToConfigIndex() == tipo && lnum == numLinha)
                {
                    return true;
                }
            }
            return false;
        }
        public static void GetNamingRulesFromTSD(out ModoNomenclatura prefix, out Separador s, out ModoNomenclatura suffix, out ModoNomenclatura nonPrefix, out bool zeros, out bool invertPrefixSuffix, ref TransportSystemDefinition tsd)

        {
            var transportType = tsd.ToConfigIndex();
            if (transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG)
            {
                suffix = ModoNomenclatura.Numero;
                s = Separador.Hifen;
                prefix = ModoNomenclatura.Romano;
                nonPrefix = ModoNomenclatura.Numero;
                zeros = false;
                invertPrefixSuffix = false;
            }
            else
            {
                suffix = (ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.SUFFIX);
                s = (Separador)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.SEPARATOR);
                prefix = (ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX);
                nonPrefix = (ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.NON_PREFIX);
                zeros = TLMCW.GetCurrentConfigBool(transportType | TLMCW.ConfigIndex.LEADING_ZEROS);
                invertPrefixSuffix = TLMCW.GetCurrentConfigBool(transportType | TLMCW.ConfigIndex.INVERT_PREFIX_SUFFIX);
            }
        }
        public static bool hasPrefix(ref TransportSystemDefinition tsd)
        {
            if (tsd == default)
            {
                return false;
            }
            var transportType = tsd.ToConfigIndex();
            return transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG || ((ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX)) != ModoNomenclatura.Nenhum;
        }

        public static bool hasPrefix(ref TransportLine t)
        {
            var tsd = TransportSystemDefinition.GetDefinitionForLine(ref t);
            if (tsd == default)
            {
                return false;
            }
            var transportType = tsd.ToConfigIndex();
            return transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG || ((ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX)) != ModoNomenclatura.Nenhum;
        }

        public static bool hasPrefix(ushort idx)
        {
            var tsd = TransportSystemDefinition.GetDefinitionForLine(idx);
            if (tsd == default)
            {
                return false;
            }
            var transportType = tsd.ToConfigIndex();
            return transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG || ((ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX)) != ModoNomenclatura.Nenhum;
        }

        public static bool hasPrefix(TransportInfo t)
        {
            var transportType = TransportSystemDefinition.From(t).ToConfigIndex();
            return transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG || ((ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX)) != ModoNomenclatura.Nenhum;
        }


        public static uint getPrefix(ushort idx)
        {
            var tsd = TransportSystemDefinition.GetDefinitionForLine(idx);
            if (tsd == default)
            {
                return 0;
            }
            var transportType = tsd.ToConfigIndex();
            if (transportType == TLMCW.ConfigIndex.EVAC_BUS_CONFIG || ((ModoNomenclatura)TLMCW.GetCurrentConfigInt(transportType | TLMCW.ConfigIndex.PREFIX)) != ModoNomenclatura.Nenhum)
            {
                uint prefix = Singleton<TransportManager>.instance.m_lines.m_buffer[idx].m_lineNumber / 1000u;
                //LogUtils.DoLog($"Prefix {prefix} for lineId {idx}");
                return prefix;
            }
            else
            {
                //LogUtils.DoLog($"Prefix 0 (def) for lineId {idx}");
                return 0;
            }
        }

        public static string getIconForLine(ushort lineIdx, bool noBorder = true)
        {
            TLMCW.ConfigIndex transportType;
            var tsd = TransportSystemDefinition.GetDefinitionForLine(lineIdx);
            transportType = tsd.ToConfigIndex();
            //if (tsd != default)
            //{
            //    transportType = tsd.toConfigIndex();
            //}
            //else
            //{
            //    transportType = TLMConfigWarehouse.ConfigIndex.TRAIN_CONFIG;
            //}
            return KlyteResourceLoader.GetDefaultSpriteNameFor(TLMUtils.GetLineIcon(TransportManager.instance.m_lines.m_buffer[lineIdx].m_lineNumber, transportType, ref tsd), noBorder);
        }


        /// <summary>
        /// </summary>
        /// <returns><c>true</c>, if recusive search for near stops was ended, <c>false</c> otherwise.</returns>
        /// <param name="pos">Position.</param>
        /// <param name="extendedMaxDistance">Max distance.</param>
        /// <param name="linesFound">Lines found.</param>
        public static bool GetNearLines(Vector3 pos, float maxDistance, ref List<ushort> linesFound)
        {
            float extendedMaxDistance = maxDistance * 1.3f;
            int num = Mathf.Max((int)((pos.x - extendedMaxDistance) / 64f + 135f), 0);
            int num2 = Mathf.Max((int)((pos.z - extendedMaxDistance) / 64f + 135f), 0);
            int num3 = Mathf.Min((int)((pos.x + extendedMaxDistance) / 64f + 135f), 269);
            int num4 = Mathf.Min((int)((pos.z + extendedMaxDistance) / 64f + 135f), 269);
            bool noneFound = true;
            NetManager nm = Singleton<NetManager>.instance;
            TransportManager tm = Singleton<TransportManager>.instance;
            for (int i = num2; i <= num4; i++)
            {
                for (int j = num; j <= num3; j++)
                {
                    ushort num6 = nm.m_nodeGrid[i * 270 + j];
                    int num7 = 0;
                    while (num6 != 0)
                    {
                        NetInfo info = nm.m_nodes.m_buffer[num6].Info;
                        if ((info.m_class.m_service == ItemClass.Service.PublicTransport))
                        {
                            ushort transportLine = nm.m_nodes.m_buffer[num6].m_transportLine;
                            var tsd = TransportSystemDefinition.GetDefinitionForLine(transportLine);
                            if (transportLine != 0 && tsd != default && TLMCW.GetCurrentConfigBool(tsd.ToConfigIndex() | TLMConfigWarehouse.ConfigIndex.SHOW_IN_LINEAR_MAP))
                            {
                                TransportInfo info2 = tm.m_lines.m_buffer[transportLine].Info;
                                if (!linesFound.Contains(transportLine) && (tm.m_lines.m_buffer[transportLine].m_flags & TransportLine.Flags.Temporary) == TransportLine.Flags.None)
                                {
                                    float num8 = Vector3.SqrMagnitude(pos - nm.m_nodes.m_buffer[num6].m_position);
                                    if (num8 < maxDistance * maxDistance || (info2.m_transportType == TransportInfo.TransportType.Ship && num8 < extendedMaxDistance * extendedMaxDistance))
                                    {
                                        linesFound.Add(transportLine);
                                        GetNearLines(nm.m_nodes.m_buffer[num6].m_position, maxDistance, ref linesFound);
                                        noneFound = false;
                                    }
                                }
                            }
                        }

                        num6 = nm.m_nodes.m_buffer[num6].m_nextGridNode;
                        if (++num7 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return noneFound;
        }



        //GetNearStopPoints
        public static bool GetNearStopPoints(Vector3 pos, float maxDistance, ref Dictionary<ushort, Vector3> stopsFound, ItemClass.SubService[] subservicesAllowed = null, int maxDepht = 4, int depth = 0)
        {
            if (depth >= maxDepht)
            {
                return false;
            }

            if (subservicesAllowed == null)
            {
                subservicesAllowed = new ItemClass.SubService[] { ItemClass.SubService.PublicTransportTrain, ItemClass.SubService.PublicTransportMetro };
            }
            int num = Mathf.Max((int)((pos.x - maxDistance) / 64f + 135f), 0);
            int num2 = Mathf.Max((int)((pos.z - maxDistance) / 64f + 135f), 0);
            int num3 = Mathf.Min((int)((pos.x + maxDistance) / 64f + 135f), 269);
            int num4 = Mathf.Min((int)((pos.z + maxDistance) / 64f + 135f), 269);
            bool noneFound = true;
            NetManager nm = Singleton<NetManager>.instance;
            TransportManager tm = Singleton<TransportManager>.instance;
            for (int i = num2; i <= num4; i++)
            {
                for (int j = num; j <= num3; j++)
                {
                    ushort stopId = nm.m_nodeGrid[i * 270 + j];
                    int num7 = 0;
                    while (stopId != 0)
                    {
                        NetInfo info = nm.m_nodes.m_buffer[stopId].Info;

                        if ((info.m_class.m_service == ItemClass.Service.PublicTransport) && subservicesAllowed.Contains(info.m_class.m_subService))
                        {
                            ushort transportLine = nm.m_nodes.m_buffer[stopId].m_transportLine;
                            if (transportLine != 0)
                            {
                                if (!stopsFound.Keys.Contains(stopId))
                                {
                                    float num8 = Vector3.SqrMagnitude(pos - nm.m_nodes.m_buffer[stopId].m_position);
                                    if (num8 < maxDistance * maxDistance)
                                    {
                                        stopsFound[stopId] = nm.m_nodes.m_buffer[stopId].m_position;
                                        GetNearStopPoints(nm.m_nodes.m_buffer[stopId].m_position, maxDistance, ref stopsFound, subservicesAllowed, maxDepht, depth + 1);
                                        noneFound = false;
                                    }
                                }
                            }
                        }

                        stopId = nm.m_nodes.m_buffer[stopId].m_nextGridNode;
                        if (++num7 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return noneFound;
        }

        /// <summary>
        /// Index the lines.
        /// </summary>
        /// <returns>The lines indexed.</returns>
        /// <param name="intersections">Intersections.</param>
        /// <param name="t">Transport line to ignore.</param>
        public static Dictionary<string, ushort> SortLines(List<ushort> intersections, TransportLine t = default)
        {
            TransportManager tm = Singleton<TransportManager>.instance;
            var otherLinesIntersections = new Dictionary<string, ushort>();
            foreach (ushort s in intersections)
            {
                TransportLine tl = tm.m_lines.m_buffer[s];
                if (t.Equals(default(TransportLine)) || tl.Info.GetSubService() != t.Info.GetSubService() || tl.m_lineNumber != t.m_lineNumber)
                {
                    var sortString = GetLineSortString(s, ref tl);
                    if (sortString != null)
                    {
                        otherLinesIntersections.Add(sortString, s);
                    }
                }
            }
            return otherLinesIntersections;
        }

        public static string GetLineSortString(ushort s, ref TransportLine tl)
        {
            string transportTypeLetter = "";
            var tsd = TransportSystemDefinition.GetDefinitionForLine(s);
            if (tsd == default)
            {
                return null;
            }
            switch (tsd.ToConfigIndex())
            {
                case TLMConfigWarehouse.ConfigIndex.PLANE_CONFIG:
                    transportTypeLetter = "A";
                    break;
                case TLMConfigWarehouse.ConfigIndex.SHIP_CONFIG:
                    transportTypeLetter = "B";
                    break;
                case TLMConfigWarehouse.ConfigIndex.BLIMP_CONFIG:
                    transportTypeLetter = "C";
                    break;
                case TLMConfigWarehouse.ConfigIndex.HELICOPTER_CONFIG:
                    transportTypeLetter = "D";
                    break;
                case TLMConfigWarehouse.ConfigIndex.TRAIN_CONFIG:
                    transportTypeLetter = "E";
                    break;
                case TLMConfigWarehouse.ConfigIndex.FERRY_CONFIG:
                    transportTypeLetter = "F";
                    break;
                case TLMConfigWarehouse.ConfigIndex.MONORAIL_CONFIG:
                    transportTypeLetter = "G";
                    break;
                case TLMConfigWarehouse.ConfigIndex.METRO_CONFIG:
                    transportTypeLetter = "H";
                    break;
                case TLMConfigWarehouse.ConfigIndex.CABLE_CAR_CONFIG:
                    transportTypeLetter = "I";
                    break;
                case TLMConfigWarehouse.ConfigIndex.TROLLEY_CONFIG:
                    transportTypeLetter = "J";
                    break;
                case TLMConfigWarehouse.ConfigIndex.TRAM_CONFIG:
                    transportTypeLetter = "K";
                    break;
                case TLMConfigWarehouse.ConfigIndex.BUS_CONFIG:
                    transportTypeLetter = "L";
                    break;
                case TLMConfigWarehouse.ConfigIndex.TOUR_BUS_CONFIG:
                    transportTypeLetter = "M";
                    break;
                case TLMConfigWarehouse.ConfigIndex.TOUR_PED_CONFIG:
                    transportTypeLetter = "N";
                    break;
            }
            return transportTypeLetter + tl.m_lineNumber.ToString().PadLeft(5, '0');
        }


        public static void PrintIntersections(string airport, string harbor, string taxi, string regionalTrainStation, string cableCarStation, UIPanel intersectionsPanel, Dictionary<string, ushort> otherLinesIntersections, Vector3 position, float scale = 1.0f, int maxItemsForSizeSwap = 3)
        {
            TransportManager tm = Singleton<TransportManager>.instance;

            int intersectionCount = otherLinesIntersections.Count;
            if (!string.IsNullOrEmpty(airport))
            {
                intersectionCount++;
            }
            if (!string.IsNullOrEmpty(harbor))
            {
                intersectionCount++;
            }
            if (!string.IsNullOrEmpty(taxi))
            {
                intersectionCount++;
            }
            if (!string.IsNullOrEmpty(regionalTrainStation))
            {
                intersectionCount++;
            }
            if (!string.IsNullOrEmpty(cableCarStation))
            {
                intersectionCount++;
            }
            float size = scale * (intersectionCount > maxItemsForSizeSwap ? 20 : 40);
            float multiplier = scale * (intersectionCount > maxItemsForSizeSwap ? 0.4f : 0.8f);
            foreach (KeyValuePair<string, ushort> s in otherLinesIntersections.OrderBy(x => x.Key))
            {
                TransportLine intersectLine = tm.m_lines.m_buffer[s.Value];
                ItemClass.SubService ss = getLineNamingParameters(s.Value, out ModoNomenclatura prefixo, out Separador separador, out ModoNomenclatura sufixo, out ModoNomenclatura naoPrefixado, out bool zeros, out bool invertPrefixSuffix, out string bgSprite).SubService;
                KlyteMonoUtils.CreateUIElement(out UIButtonLineInfo lineCircleIntersect, intersectionsPanel.transform);
                lineCircleIntersect.autoSize = false;
                lineCircleIntersect.width = size;
                lineCircleIntersect.height = size;
                lineCircleIntersect.color = intersectLine.m_color;
                lineCircleIntersect.pivot = UIPivotPoint.MiddleLeft;
                lineCircleIntersect.verticalAlignment = UIVerticalAlignment.Middle;
                lineCircleIntersect.name = "LineFormat";
                lineCircleIntersect.relativePosition = new Vector3(0f, 0f);
                lineCircleIntersect.normalBgSprite = bgSprite;
                lineCircleIntersect.hoveredColor = Color.white;
                lineCircleIntersect.hoveredTextColor = Color.red;
                lineCircleIntersect.lineID = s.Value;
                lineCircleIntersect.tooltip = tm.GetLineName(s.Value);
                lineCircleIntersect.eventClick += (x, y) =>
                {
                    InstanceID iid = InstanceID.Empty;
                    iid.TransportLine = s.Value;
                    WorldInfoPanel.Show<PublicTransportWorldInfoPanel>(position, iid);

                };
                KlyteMonoUtils.CreateUIElement(out UILabel lineNumberIntersect, lineCircleIntersect.transform);
                lineNumberIntersect.autoSize = false;
                lineNumberIntersect.autoHeight = false;
                lineNumberIntersect.width = lineCircleIntersect.width;
                lineNumberIntersect.pivot = UIPivotPoint.MiddleCenter;
                lineNumberIntersect.textAlignment = UIHorizontalAlignment.Center;
                lineNumberIntersect.verticalAlignment = UIVerticalAlignment.Middle;
                lineNumberIntersect.name = "LineNumber";
                lineNumberIntersect.height = size;
                lineNumberIntersect.relativePosition = new Vector3(-0.5f, 0.5f);
                lineNumberIntersect.outlineColor = Color.black;
                lineNumberIntersect.useOutline = true;
                getLineActive(ref intersectLine, out bool day, out bool night);
                bool zeroed;
                unchecked
                {
                    zeroed = (tm.m_lines.m_buffer[s.Value].m_flags & (TransportLine.Flags)TLMTransportLineFlags.ZERO_BUDGET_CURRENT) != 0;
                }
                if (!day || !night || zeroed)
                {
                    KlyteMonoUtils.CreateUIElement(out UILabel daytimeIndicator, lineCircleIntersect.transform);
                    daytimeIndicator.autoSize = false;
                    daytimeIndicator.width = size;
                    daytimeIndicator.height = size;
                    daytimeIndicator.color = Color.white;
                    daytimeIndicator.pivot = UIPivotPoint.MiddleLeft;
                    daytimeIndicator.verticalAlignment = UIVerticalAlignment.Middle;
                    daytimeIndicator.name = "LineTime";
                    daytimeIndicator.relativePosition = new Vector3(0f, 0f);
                    /*TODO: !!!!!! */
                    daytimeIndicator.backgroundSprite = zeroed ? "NoBudgetIcon" : day ? "DayIcon" : night ? "NightIcon" : "DisabledIcon";
                }
                setLineNumberCircleOnRef(s.Value, lineNumberIntersect);
                lineNumberIntersect.textScale *= multiplier;
                lineNumberIntersect.relativePosition *= multiplier;
            }
            if (airport != string.Empty)
            {
                addExtraStationBuildingIntersection(intersectionsPanel, size, "AirplaneIcon", airport);
            }
            if (harbor != string.Empty)
            {
                addExtraStationBuildingIntersection(intersectionsPanel, size, "ShipIcon", harbor);
            }
            if (taxi != string.Empty)
            {
                addExtraStationBuildingIntersection(intersectionsPanel, size, "TaxiIcon", taxi);
            }
            if (regionalTrainStation != string.Empty)
            {
                addExtraStationBuildingIntersection(intersectionsPanel, size, "RegionalTrainIcon", regionalTrainStation);
            }
            if (cableCarStation != string.Empty)
            {
                addExtraStationBuildingIntersection(intersectionsPanel, size, "CableCarIcon", cableCarStation);
            }
        }


        public static void getLineActive(ref TransportLine t, out bool day, out bool night)
        {
            day = (t.m_flags & TransportLine.Flags.DisabledDay) == TransportLine.Flags.None;
            night = (t.m_flags & TransportLine.Flags.DisabledNight) == TransportLine.Flags.None;
        }

        public static void setLineActive(ref TransportLine t, bool day, bool night) => t.SetActive(day, night);

        private static void addExtraStationBuildingIntersection(UIComponent parent, float size, string bgSprite, string description)
        {
            KlyteMonoUtils.CreateUIElement(out UILabel lineCircleIntersect, parent.transform);
            lineCircleIntersect.autoSize = false;
            lineCircleIntersect.width = size;
            lineCircleIntersect.height = size;
            lineCircleIntersect.pivot = UIPivotPoint.MiddleLeft;
            lineCircleIntersect.verticalAlignment = UIVerticalAlignment.Middle;
            lineCircleIntersect.name = "LineFormat";
            lineCircleIntersect.relativePosition = new Vector3(0f, 0f);
            lineCircleIntersect.backgroundSprite = bgSprite;
            lineCircleIntersect.tooltip = description;
        }

        public static void setLineNumberCircleOnRef(ushort lineID, UITextComponent reference, float ratio = 1f)
        {
            getLineNumberCircleOnRefParams(lineID, ratio, out string text, out Color textColor, out float textScale, out Vector3 relativePosition);
            reference.text = text;
            reference.textScale = textScale;
            reference.relativePosition = relativePosition;
            reference.textColor = textColor;
            reference.useOutline = true;
            reference.outlineColor = Color.black;
        }

        private static void getLineNumberCircleOnRefParams(ushort lineID, float ratio, out string text, out Color textColor, out float textScale, out Vector3 relativePosition)
        {
            text = getLineStringId(lineID).Trim();
            string[] textParts = text.Split(new char[] { '\n' });
            int lenght = textParts.Max(x => x.Length);
            if (lenght >= 9 && textParts.Length == 1)
            {
                text = text.Replace("·", "\n").Replace(".", "\n").Replace("-", "\n").Replace("/", "\n").Replace(" ", "\n");
                textParts = text.Split(new char[] { '\n' });
                lenght = textParts.Max(x => x.Length);
            }
            if (lenght >= 8)
            {
                textScale = 0.4f * ratio;
                relativePosition = new Vector3(0f, 0.125f);
            }
            else if (lenght >= 6)
            {
                textScale = 0.666f * ratio;
                relativePosition = new Vector3(0f, 0.5f);
            }
            else if (lenght >= 4)
            {
                textScale = 1f * ratio;
                relativePosition = new Vector3(0f, 1f);
            }
            else if (lenght == 3 || textParts.Length > 1)
            {
                textScale = 1.25f * ratio;
                relativePosition = new Vector3(0f, 1.5f);
            }
            else if (lenght == 2)
            {
                textScale = 1.75f * ratio;
                relativePosition = new Vector3(-0.5f, 0.5f);
            }
            else
            {
                textScale = 2.3f * ratio;
                relativePosition = new Vector3(-0.5f, 0f);
            }
            textColor = TLMTransportLineExtension.Instance.IsUsingCustomConfig(lineID) ? Color.yellow : Color.white;
        }

        public static int getVehicleCapacity(ushort vehicleId)
        {
            PrefabAI ai = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].Info.GetAI();
            if (ai as BusAI != null)
            {
                return (ai as BusAI).m_passengerCapacity;
            }
            if (ai as PassengerPlaneAI != null)
            {
                return (ai as PassengerPlaneAI).m_passengerCapacity;
            }
            if (ai as PassengerShipAI != null)
            {
                return (ai as PassengerShipAI).m_passengerCapacity;
            }
            if (ai as PassengerFerryAI != null)
            {
                return (ai as PassengerFerryAI).m_passengerCapacity;
            }
            if (ai as PassengerBlimpAI != null)
            {
                return (ai as PassengerBlimpAI).m_passengerCapacity;
            }
            if (ai as CableCarAI != null)
            {
                return (ai as CableCarAI).m_passengerCapacity;
            }
            //if (ai as MonorailAI != null)
            //{
            //    return (ai as MonorailAI).m_passengerCapacity;
            //}
            if (ai as PassengerTrainAI != null)
            {
                return (ai as PassengerTrainAI).m_passengerCapacity;
            }
            if (ai as TaxiAI != null)
            {
                return (ai as TaxiAI).m_passengerCapacity;
            }
            if (ai as TramAI != null)
            {
                return (ai as TramAI).m_passengerCapacity;
            }
            return 0;
        }

        public static string[] getAllStopsFromLine(ushort lineID)
        {
            TransportLine t = TransportManager.instance.m_lines.m_buffer[lineID];
            int stopsCount = t.CountStops(lineID);
            string[] result = new string[stopsCount];
            ItemClass.SubService ss = TransportSystemDefinition.GetDefinitionForLine(lineID).SubService;
            for (int i = 0; i < stopsCount; i++)
            {
                ushort stationId = t.GetStop(i);
                result[i] = TLMLineUtils.getFullStationName(stationId, lineID, ss);
            }
            return result;
        }


        #region Line Utils
        public static AsyncTask<bool> setLineColor(ushort lineIdx, Color color) => Singleton<SimulationManager>.instance.AddAction<bool>(TransportManager.instance.SetLineColor(lineIdx, color));
        public static AsyncTask<bool> setLineName(ushort lineIdx, string name) => Singleton<SimulationManager>.instance.AddAction<bool>(TransportManager.instance.SetLineName(lineIdx, name));

        private static TransportInfo.TransportType[] roadTransportTypes = new TransportInfo.TransportType[] { TransportInfo.TransportType.Bus, TransportInfo.TransportType.Tram };

        public static string CalculateAutoName(ushort lineIdx, out ushort startStation, out ushort endStation, out string startStationStr, out string endStationStr)
        {
            ref TransportLine t = ref Singleton<TransportManager>.instance.m_lines.m_buffer[lineIdx];
            if ((t.m_flags & TransportLine.Flags.Complete) == TransportLine.Flags.None)
            {
                startStation = 0;
                endStation = 0;
                startStationStr = null;
                endStationStr = null;
                return null;
            }
            ushort nextStop = t.m_stops;
            bool allowPrefixInStations = roadTransportTypes.Contains(t.Info.m_transportType);
            var stations = new List<Tuple<NamingType, string, ushort>>();
            do
            {
                NetNode stopNode = NetManager.instance.m_nodes.m_buffer[nextStop];
                string stationName = getStationName(nextStop, lineIdx, t.Info.m_class.m_subService, out ItemClass.Service serviceFound, out ItemClass.SubService subserviceFound, out string prefixFound, out ushort buildingId, out NamingType namingType, true, true);
                var tuple = Tuple.New(namingType, allowPrefixInStations ? $"{prefixFound?.Trim()} {stationName?.Trim()}".Trim() : stationName, nextStop);
                stations.Add(tuple);
                nextStop = TransportLine.GetNextStop(nextStop);
            } while (nextStop != t.m_stops && nextStop != 0);
            string prefix = "";
            if (TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.ADD_LINE_NUMBER_IN_AUTONAME))
            {
                prefix = $"[{getLineStringId(lineIdx)}] ";
            }
            TLMUtils.doLog($"stations => [{string.Join(" ; ", stations.Select(x => $"{x.First}|{x.Second}").ToArray())}]");
            if (stations.Count % 2 == 0 && stations.Count > 2)
            {
                TLMUtils.doLog($"Try Simmetric");
                int middle = -1;
                for (int i = 1; i <= stations.Count / 2; i++)
                {
                    if (stations[i - 1].First == stations[i + 1].First && stations[i - 1].Second == stations[i + 1].Second)
                    {
                        middle = i;
                        break;
                    }
                }
                TLMUtils.doLog($"middle => {middle}");
                if (middle != -1)
                {
                    bool simmetric = true;
                    middle += stations.Count;
                    for (int i = 1; i < stations.Count / 2; i++)
                    {
                        Tuple<NamingType, string> A = stations[(middle + i) % stations.Count];
                        Tuple<NamingType, string> B = stations[(middle - i) % stations.Count];
                        if (A.First != B.First || A.Second != B.Second)
                        {
                            simmetric = false;
                            break;
                        }
                    }
                    if (simmetric)
                    {
                        startStation = stations[middle % stations.Count].Third;
                        endStation = stations[(middle + (stations.Count / 2)) % stations.Count].Third;

                        startStationStr = stations[(middle) % stations.Count].Second;
                        endStationStr = stations[(middle + stations.Count / 2) % stations.Count].Second;

                        if (startStationStr == endStationStr)
                        {
                            startStationStr = (TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.CIRCULAR_IN_SINGLE_DISTRICT_LINE) ? "Circular " : "") + startStationStr;
                            endStationStr = startStationStr;
                            return $"{prefix}{startStationStr}";
                        }
                        else
                        {
                            return $"{prefix}{startStationStr} - {endStationStr}";
                        }                        
                    }
                }
            }
            var idxStations = stations.Select((x, y) => Tuple.New(y, x.First, x.Second, x.Third)).OrderBy(x => x.Second.GetNamePrecedenceRate()).ToList();

            int targetStart = 0;
            int mostRelevantEndIdx = -1;
            int j = 0;
            int maxDistanceEnd = (int)(idxStations.Count / 8f + 0.5f);
            TLMUtils.doLog("idxStations");
            do
            {
                Tuple<int, NamingType, string> peerCandidate = idxStations.Where(x => x.Third != idxStations[j].Third && Math.Abs((x.First < idxStations[j].First ? x.First + idxStations.Count : x.First) - idxStations.Count / 2 - idxStations[j].First) <= maxDistanceEnd).OrderBy(x => x.Second.GetNamePrecedenceRate()).FirstOrDefault();
                if (peerCandidate != null && (mostRelevantEndIdx == -1 || stations[mostRelevantEndIdx].First.GetNamePrecedenceRate() > peerCandidate.Second.GetNamePrecedenceRate()))
                {
                    targetStart = j;
                    mostRelevantEndIdx = peerCandidate.First;
                }
                j++;
            } while (j < idxStations.Count && idxStations[j].Second.GetNamePrecedenceRate() == idxStations[0].Second.GetNamePrecedenceRate());

            if (mostRelevantEndIdx >= 0)
            {
                startStation = idxStations[targetStart].Fourth;
                endStation = stations[mostRelevantEndIdx].Third;
                startStationStr = idxStations[targetStart].Third;
                endStationStr = stations[mostRelevantEndIdx].Second;
                if (startStationStr == endStationStr)
                {
                    startStationStr = (TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.CIRCULAR_IN_SINGLE_DISTRICT_LINE) ? "Circular " : "") + startStationStr;
                    endStationStr = startStationStr;
                    return $"{prefix}{startStationStr}";
                }
                else
                {
                    return $"{prefix}{startStationStr} - {endStationStr}";
                }
            }
            else
            {
                startStation = idxStations[0].Fourth;
                endStation = 0;
                startStationStr = (TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.CIRCULAR_IN_SINGLE_DISTRICT_LINE) ? "Circular " : "") + idxStations[0].Third;
                endStationStr = null;
                return prefix + startStationStr;
            }

        }

        private static string GetStationNameWithPrefix(TLMCW.ConfigIndex transportType, string name) => transportType.GetSystemStationNamePrefix().Trim() + (transportType.GetSystemStationNamePrefix().Trim() != string.Empty ? " " : "") + name;

        public static void setStopName(string newName, ushort stopId, ushort lineId, Action callback)
        {
            TLMUtils.doLog("setStopName! {0} - {1} - {2}", newName, stopId, lineId);
            ushort buildingId = getStationBuilding(stopId, Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].Info.m_class.m_subService, true, true);
            if (buildingId == 0)
            {
                TLMUtils.doLog("b=0");
                Singleton<BuildingManager>.instance.StartCoroutine(SetNodeName(stopId, newName, callback));
            }
            else
            {
                TLMUtils.doLog("b≠0 ({0})", buildingId);
                Singleton<BuildingManager>.instance.StartCoroutine(BuildingUtils.SetBuildingName(buildingId, newName, callback));
            }
        }

        public static IEnumerator<object> SetNodeName(ushort nodeId, string name, Action function)
        {
            yield return Singleton<SimulationManager>.instance.AddAction<bool>(SetNodeName(nodeId, name));
            function();
        }
        public static string GetStopName(ushort stopId)
        {
            InstanceID id = default;
            id.NetNode = stopId;
            return InstanceManager.instance.GetName(id);
        }

        private static IEnumerator<bool> SetNodeName(ushort nodeId, string name)
        {
            bool result = false;
            NetNode.Flags flags = NetManager.instance.m_nodes.m_buffer[nodeId].m_flags;
            TLMUtils.doLog($"SetNodeName({nodeId},{name}) {flags}");
            if (nodeId != 0 && flags != NetNode.Flags.None)
            {
                var id = default(InstanceID);
                id.NetNode = nodeId;
                Singleton<InstanceManager>.instance.SetName(id, name.IsNullOrWhiteSpace() ? null : name);
                result = true;
            }
            yield return result;
            yield break;

        }
        public static bool CalculateSimmetry(ItemClass.SubService ss, int stopsCount, TransportLine t, out int middle)
        {
            int j;
            NetManager nm = Singleton<NetManager>.instance;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            middle = -1;
            if ((t.m_flags & (TransportLine.Flags.Invalid | TransportLine.Flags.Temporary)) != TransportLine.Flags.None)
            {
                return false;
            }
            //try to find the loop
            for (j = -1; j < stopsCount / 2; j++)
            {
                int offsetL = (j + stopsCount) % stopsCount;
                int offsetH = (j + 2) % stopsCount;
                NetNode nn1 = nm.m_nodes.m_buffer[t.GetStop(offsetL)];
                NetNode nn2 = nm.m_nodes.m_buffer[t.GetStop(offsetH)];
                ushort buildingId1 = BuildingUtils.FindBuilding(nn1.m_position, 100f, ItemClass.Service.PublicTransport, ss, TLMUtils.defaultAllowedVehicleTypes, Building.Flags.None, Building.Flags.Untouchable);
                ushort buildingId2 = BuildingUtils.FindBuilding(nn2.m_position, 100f, ItemClass.Service.PublicTransport, ss, TLMUtils.defaultAllowedVehicleTypes, Building.Flags.None, Building.Flags.Untouchable);
                //					TLMUtils.doLog("buildingId1="+buildingId1+"|buildingId2="+buildingId2);
                //					TLMUtils.doLog("offsetL="+offsetL+"|offsetH="+offsetH);
                if (buildingId1 == buildingId2)
                {
                    middle = j + 1;
                    break;
                }
            }
            //				TLMUtils.doLog("middle="+middle);
            if (middle >= 0)
            {
                for (j = 1; j <= stopsCount / 2; j++)
                {
                    int offsetL = (-j + middle + stopsCount) % stopsCount;
                    int offsetH = (j + middle) % stopsCount;
                    //						TLMUtils.doLog("offsetL="+offsetL+"|offsetH="+offsetH);
                    //						TLMUtils.doLog("t.GetStop (offsetL)="+t.GetStop (offsetH)+"|t.GetStop (offsetH)="+t.GetStop (offsetH));
                    NetNode nn1 = nm.m_nodes.m_buffer[t.GetStop(offsetL)];
                    NetNode nn2 = nm.m_nodes.m_buffer[t.GetStop(offsetH)];
                    ushort buildingId1 = BuildingUtils.FindBuilding(nn1.m_position, 100f, ItemClass.Service.PublicTransport, ss, TLMUtils.defaultAllowedVehicleTypes, Building.Flags.None, Building.Flags.Untouchable);
                    ushort buildingId2 = BuildingUtils.FindBuilding(nn2.m_position, 100f, ItemClass.Service.PublicTransport, ss, TLMUtils.defaultAllowedVehicleTypes, Building.Flags.None, Building.Flags.Untouchable);
                    //						TLMUtils.doLog("buildingId1="+buildingId1+"|buildingId2="+buildingId2);
                    //						TLMUtils.doLog("buildingId1="+buildingId1+"|buildingId2="+buildingId2);
                    //						TLMUtils.doLog("offsetL="+offsetL+"|offsetH="+offsetH);
                    if (buildingId1 != buildingId2)
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                return false;
            }

        }
        public static string getStationName(ushort stopId, ushort lineId, ItemClass.SubService ss, out ItemClass.Service serviceFound, out ItemClass.SubService subserviceFound, out string prefix, out ushort buildingID, out NamingType resultNamingType, bool excludeCargo = false, bool useRestrictionForAreas = false)
        {
            string savedName = GetStopName(stopId);
            var tsd = TransportSystemDefinition.From(lineId);
            if (savedName != null)
            {
                serviceFound = ItemClass.Service.PublicTransport;
                subserviceFound = Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].Info.m_class.m_subService;
                prefix = tsd.ToConfigIndex().GetSystemStationNamePrefix(lineId)?.TrimStart();
                buildingID = 0;
                resultNamingType = NamingTypeExtensions.From(serviceFound, subserviceFound);
                return savedName;
            }

            NetManager nm = Singleton<NetManager>.instance;
            NetNode nn = nm.m_nodes.m_buffer[stopId];
            Vector3 location = nn.m_position;
            if (tsd.VehicleType == VehicleInfo.VehicleType.Car || tsd.VehicleType == VehicleInfo.VehicleType.Tram)
            {
                List<ushort> nearStops = StopSearchUtils.FindNearStops(location, nn.Info.GetService(), true, 50f, out _, out _);

                foreach (ushort otherStop in nearStops)
                {
                    if (otherStop != stopId)
                    {
                        savedName = GetStopName(otherStop);
                        ;
                        if (savedName != null)
                        {
                            ushort targetLineId = NetManager.instance.m_nodes.m_buffer[otherStop].m_transportLine;
                            var tsd2 = TransportSystemDefinition.From(targetLineId);

                            serviceFound = ItemClass.Service.PublicTransport;
                            subserviceFound = Singleton<TransportManager>.instance.m_lines.m_buffer[targetLineId].Info.m_class.m_subService;
                            prefix = tsd2.ToConfigIndex().GetSystemStationNamePrefix(targetLineId)?.TrimStart();
                            buildingID = 0;
                            resultNamingType = NamingTypeExtensions.From(serviceFound, subserviceFound);
                            return savedName;
                        }
                    }
                }
            }

            buildingID = getStationBuilding(stopId, ss, excludeCargo);

            if (buildingID > 0)
            {
                string name = TLMUtils.getBuildingName(buildingID, out serviceFound, out subserviceFound, out prefix, lineId);
                resultNamingType = NamingTypeExtensions.From(serviceFound, subserviceFound);
                return name;
            }


            prefix = "";
            byte parkId = DistrictManager.instance.GetPark(location);
            if (parkId > 0)
            {
                var idx = DistrictManager.instance.m_parks.m_buffer[parkId].ToConfigIndex();
                if (!useRestrictionForAreas || TLMCW.GetCurrentConfigBool(idx | TLMConfigWarehouse.ConfigIndex.USE_FOR_AUTO_NAMING_REF))
                {
                    prefix = idx.GetSystemStationNamePrefix(lineId)?.TrimStart();
                    serviceFound = idx.ToServiceSubservice(out subserviceFound);
                    resultNamingType = idx switch
                    {
                        CIdx.CAMPUS_AREA_NAME_CONFIG => NamingType.CAMPUS,
                        CIdx.INDUSTRIAL_AREA_NAME_CONFIG => NamingType.INDUSTRY_AREA,
                        _ => NamingType.PARKAREA,
                    };
                    return DistrictManager.instance.GetParkName(parkId);
                }
            }
            if (SegmentUtils.GetAddressStreetAndNumber(location, location, out int number, out string streetName) && (!useRestrictionForAreas || TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.ADDRESS_NAME_CONFIG | TLMConfigWarehouse.ConfigIndex.USE_FOR_AUTO_NAMING_REF)) && !string.IsNullOrEmpty(streetName))
            {
                prefix = TLMCW.ConfigIndex.ADDRESS_NAME_CONFIG.GetSystemStationNamePrefix(lineId)?.TrimStart();
                serviceFound = ItemClass.Service.Road;
                subserviceFound = ItemClass.SubService.PublicTransportBus;
                resultNamingType = NamingType.ADDRESS;
                return streetName + ", " + number;

            }
            else if (DistrictManager.instance.GetDistrict(location) > 0 && (!useRestrictionForAreas || TLMCW.GetCurrentConfigBool(TLMCW.ConfigIndex.DISTRICT_NAME_CONFIG | TLMConfigWarehouse.ConfigIndex.USE_FOR_AUTO_NAMING_REF)))
            {
                prefix = TLMCW.ConfigIndex.DISTRICT_NAME_CONFIG.GetSystemStationNamePrefix(lineId)?.TrimStart();
                serviceFound = ItemClass.Service.Natural;
                subserviceFound = ItemClass.SubService.None;
                resultNamingType = NamingType.DISTRICT;
                return DistrictManager.instance.GetDistrictName(DistrictManager.instance.GetDistrict(location));
            }
            else
            {
                serviceFound = ItemClass.Service.None;
                subserviceFound = ItemClass.SubService.None;
                resultNamingType = NamingType.NONE;
                return "????????";
            }
        }
        public static readonly ItemClass.Service[] searchOrder = new ItemClass.Service[]{
            ItemClass.Service.PublicTransport,
            ItemClass.Service.Monument,
            ItemClass.Service.Beautification,
            ItemClass.Service.Natural,
            ItemClass.Service.Disaster,
            ItemClass.Service.HealthCare,
            ItemClass.Service.FireDepartment,
            ItemClass.Service.PoliceDepartment,
            ItemClass.Service.Tourism,
            ItemClass.Service.Education,
            ItemClass.Service.Garbage,
            ItemClass.Service.Office,
            ItemClass.Service.Commercial,
            ItemClass.Service.Industrial,
            ItemClass.Service.Residential,
            ItemClass.Service.Electricity,
            ItemClass.Service.Water
        };
        public static string getStationName(ushort stopId, ushort lineId, ItemClass.SubService ss) => getStationName(stopId, lineId, ss, out ItemClass.Service serv, out ItemClass.SubService subServ, out string prefix, out ushort buildingId, out NamingType namingType, true);
        public static string getFullStationName(ushort stopId, ushort lineId, ItemClass.SubService ss)
        {
            string result = getStationName(stopId, lineId, ss, out ItemClass.Service serv, out ItemClass.SubService subServ, out string prefix, out ushort buildingId, out NamingType namingType, true);
            return string.IsNullOrEmpty(prefix) ? result : prefix + " " + result;
        }
        public static Vector3 getStationBuildingPosition(uint stopId, ItemClass.SubService ss)
        {
            ushort buildingId = getStationBuilding(stopId, ss);


            if (buildingId > 0)
            {
                BuildingManager bm = Singleton<BuildingManager>.instance;
                Building b = bm.m_buildings.m_buffer[buildingId];
                InstanceID iid = default;
                iid.Building = buildingId;
                return b.m_position;
            }
            else
            {
                NetManager nm = Singleton<NetManager>.instance;
                NetNode nn = nm.m_nodes.m_buffer[(int)stopId];
                return nn.m_position;
            }
        }

        //ORDEM DE BUSCA DE CONFIG
        private static CIdx[] searchOrderStationNamingRule = new CIdx[] {
        CIdx.PLANE_USE_FOR_AUTO_NAMING_REF                  ,
        CIdx.SHIP_USE_FOR_AUTO_NAMING_REF                   ,
        CIdx.BLIMP_USE_FOR_AUTO_NAMING_REF                  ,
        CIdx.FERRY_USE_FOR_AUTO_NAMING_REF                  ,
        CIdx.CABLE_CAR_USE_FOR_AUTO_NAMING_REF              ,
        CIdx.TRAIN_USE_FOR_AUTO_NAMING_REF                  ,
        CIdx.METRO_USE_FOR_AUTO_NAMING_REF                  ,
        CIdx.MONORAIL_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.TRAM_USE_FOR_AUTO_NAMING_REF                   ,
        CIdx.BUS_USE_FOR_AUTO_NAMING_REF                    ,
        CIdx.TOUR_PED_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.TOUR_BUS_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.BALOON_USE_FOR_AUTO_NAMING_REF                 ,
        CIdx.TAXI_USE_FOR_AUTO_NAMING_REF                   ,
        CIdx.PUBLICTRANSPORT_USE_FOR_AUTO_NAMING_REF        ,
        CIdx.MONUMENT_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.BEAUTIFICATION_USE_FOR_AUTO_NAMING_REF         ,
        CIdx.TOURISM_USE_FOR_AUTO_NAMING_REF                ,
        CIdx.NATURAL_USE_FOR_AUTO_NAMING_REF                ,
        CIdx.DISASTER_USE_FOR_AUTO_NAMING_REF             ,
        CIdx.HEALTHCARE_USE_FOR_AUTO_NAMING_REF             ,
        CIdx.FIREDEPARTMENT_USE_FOR_AUTO_NAMING_REF         ,
        CIdx.POLICEDEPARTMENT_USE_FOR_AUTO_NAMING_REF       ,
        CIdx.EDUCATION_USE_FOR_AUTO_NAMING_REF              ,
        CIdx.GARBAGE_USE_FOR_AUTO_NAMING_REF                ,
        CIdx.ROAD_USE_FOR_AUTO_NAMING_REF                   ,
        CIdx.CITIZEN_USE_FOR_AUTO_NAMING_REF                ,
        CIdx.ELECTRICITY_USE_FOR_AUTO_NAMING_REF            ,
        CIdx.WATER_USE_FOR_AUTO_NAMING_REF                  ,

        CIdx.OFFICE_USE_FOR_AUTO_NAMING_REF                 ,
        CIdx.COMMERCIAL_USE_FOR_AUTO_NAMING_REF             ,
        CIdx.INDUSTRIAL_USE_FOR_AUTO_NAMING_REF             ,
        CIdx.RESIDENTIAL_USE_FOR_AUTO_NAMING_REF            ,

        //CIdx.UNUSED2_USE_FOR_AUTO_NAMING_REF                ,
        CIdx.CAMPUS_AREA_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.PARKAREA_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.INDUSTRIAL_AREA_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.DISTRICT_USE_FOR_AUTO_NAMING_REF               ,
        CIdx.ADDRESS_USE_FOR_AUTO_NAMING_REF                ,
        };

        public static ushort getStationBuilding(uint stopId, ItemClass.SubService ss, bool excludeCargo = false, bool restrictToTransportType = false)
        {
            NetManager nm = Singleton<NetManager>.instance;
            BuildingManager bm = Singleton<BuildingManager>.instance;
            NetNode nn = nm.m_nodes.m_buffer[(int)stopId];
            ushort tempBuildingId;


            if (ss != ItemClass.SubService.None)
            {
                tempBuildingId = BuildingUtils.FindBuilding(nn.m_position, 100f, ItemClass.Service.PublicTransport, ss, TLMUtils.defaultAllowedVehicleTypes, Building.Flags.None, Building.Flags.None);
                if (IsBuildingValidForStation(excludeCargo, bm, tempBuildingId))
                {
                    var parent = Building.FindParentBuilding(tempBuildingId);
                    return parent == 0 ? tempBuildingId : parent;
                }
            }
            if (!restrictToTransportType)
            {
                if (nn.m_transportLine > 0)
                {
                    tempBuildingId = BuildingUtils.FindBuilding(nn.m_position, 100f, ItemClass.Service.PublicTransport, ItemClass.SubService.None, TLMCW.getTransferReasonFromSystemId(TransportSystemDefinition.From(TransportManager.instance.m_lines.m_buffer[nn.m_transportLine].Info).ToConfigIndex()), Building.Flags.None, Building.Flags.None);
                    if (IsBuildingValidForStation(excludeCargo, bm, tempBuildingId))
                    {
                        var parent = Building.FindParentBuilding(tempBuildingId);
                        return parent == 0 ? tempBuildingId : parent;
                    }
                }


                foreach (CIdx idx in searchOrderStationNamingRule)
                {
                    if (TLMCW.GetCurrentConfigBool(idx))
                    {
                        tempBuildingId = BuildingUtils.FindBuilding(nn.m_position, 100f, (ItemClass.Service)((int)idx & (int)CIdx.DESC_DATA), TLMCW.getSubserviceFromSystemId(idx), null, Building.Flags.None, Building.Flags.None);
                        if (IsBuildingValidForStation(excludeCargo, bm, tempBuildingId))
                        {
                            var parent = Building.FindParentBuilding(tempBuildingId);
                            return parent == 0 ? tempBuildingId : parent;
                        }
                    }

                }
            }
            return 0;

        }
        public static Tuple<string, Color, string> GetIconStringParameters(ushort lineId) => Tuple.New(getIconForLine(lineId, false), Singleton<TransportManager>.instance.GetLineColor(lineId), getLineStringId(lineId));
        internal static string GetIconString(ushort lineId) => GetIconString(getIconForLine(lineId, false), Singleton<TransportManager>.instance.GetLineColor(lineId), getLineStringId(lineId));
        internal static string GetIconString(string iconName, Color color, string lineString) => $"<{UIDynamicFontRendererRedirector.TAG_LINE} {iconName},{color.ToRGB()},{lineString}>";

        private static bool IsBuildingValidForStation(bool excludeCargo, BuildingManager bm, ushort tempBuildingId)
        {
            var ai = bm.m_buildings.m_buffer[tempBuildingId].Info.m_buildingAI;
            return tempBuildingId > 0 && ((!excludeCargo && (ai is DepotAI || ai is CargoStationAI)) || ai is TransportStationAI || ai is OutsideConnectionAI);
        }

        public static int CalculateTargetVehicleCount(ref TransportLine t, ushort lineId, float lineLength) => CalculateTargetVehicleCount(t.Info, lineLength, GetEffectiveBudget(lineId));
        public static int CalculateTargetVehicleCount(TransportInfo info, float lineLength, float budget) => Mathf.CeilToInt(budget * lineLength / info.m_defaultVehicleDistance);
        public static float CalculateBudgetForEachVehicle(TransportInfo info, float lineLength) => info.m_defaultVehicleDistance / lineLength;

        public static int GetTicketPriceForVehicle(VehicleAI ai, ushort vehicleID, ref Vehicle vehicleData)
        {
            var def = TransportSystemDefinition.From(vehicleData.Info);

            if (def == default)
            {
                LogUtils.DoLog($"GetTicketPriceForVehicle ({vehicleID}):DEFAULT TSD FOR {ai}");
                return ai.GetTicketPrice(vehicleID, ref vehicleData);
            }

            DistrictManager instance = Singleton<DistrictManager>.instance;
            byte district = instance.GetDistrict(vehicleData.m_targetPos3);
            DistrictPolicies.Services servicePolicies = instance.m_districts.m_buffer[district].m_servicePolicies;
            DistrictPolicies.Event @event = instance.m_districts.m_buffer[district].m_eventPolicies & Singleton<EventManager>.instance.GetEventPolicyMask();
            float multiplier;
            if (vehicleData.Info.m_class.m_subService == ItemClass.SubService.PublicTransportTours)
            {
                multiplier = 1;
            }
            else
            {
                if ((servicePolicies & DistrictPolicies.Services.FreeTransport) != DistrictPolicies.Services.None)
                {
                    LogUtils.DoLog($"GetTicketPriceForVehicle ({vehicleID}): FreeTransport at district!");
                    return 0;
                }
                if ((@event & DistrictPolicies.Event.ComeOneComeAll) != DistrictPolicies.Event.None)
                {
                    LogUtils.DoLog($"GetTicketPriceForVehicle ({vehicleID}): ComeOneComeAll at district!");
                    return 0;
                }
                if ((servicePolicies & DistrictPolicies.Services.HighTicketPrices) != DistrictPolicies.Services.None)
                {
                    LogUtils.DoLog($"GetTicketPriceForVehicle ({vehicleID}): HighTicketPrices at district!");
                    instance.m_districts.m_buffer[district].m_servicePoliciesEffect = (instance.m_districts.m_buffer[district].m_servicePoliciesEffect | DistrictPolicies.Services.HighTicketPrices);
                    multiplier = 5f / 4f;
                }
                else
                {
                    multiplier = 1;
                }
            }
            uint ticketPriceDefault = GetTicketPriceForLine(ref def, vehicleData.m_transportLine).First.Value;
            LogUtils.DoLog($"GetTicketPriceForVehicle ({vehicleID}): multiplier = {multiplier}, ticketPriceDefault = {ticketPriceDefault}");

            return (int)(multiplier * ticketPriceDefault);

        }

        public static Tuple<TicketPriceEntryXml, int> GetTicketPriceForLine(ref TransportSystemDefinition tsd, ushort lineId) => GetTicketPriceForLine(ref tsd, lineId, SimulationManager.instance.m_currentDayTimeHour);
        public static Tuple<TicketPriceEntryXml, int> GetTicketPriceForLine(ref TransportSystemDefinition tsd, ushort lineId, float hour)
        {
            Tuple<TicketPriceEntryXml, int> ticketPriceDefault = null;
            if (lineId > 0)
            {
                if (TLMTransportLineExtension.Instance.IsUsingCustomConfig(lineId))
                {
                    ticketPriceDefault = TLMTransportLineExtension.Instance.GetTicketPriceForHourForLine(lineId, hour);
                }
                if ((ticketPriceDefault?.First?.Value ?? 0) == 0)
                {
                    ticketPriceDefault = tsd.GetTransportExtension().GetTicketPriceForHourForLine(lineId, hour);
                }

            }
            if ((ticketPriceDefault?.First?.Value ?? 0) == 0)
            {
                ticketPriceDefault = Tuple.New(new TicketPriceEntryXml() { Value = (uint)TLMCW.GetSettedTicketPrice(tsd.ToConfigIndex()) }, -1);
            }
            if ((ticketPriceDefault?.First?.Value ?? 0) == 0)
            {
                ticketPriceDefault = Tuple.New(new TicketPriceEntryXml() { Value = (uint)TransportManager.instance.m_lines.m_buffer[lineId].Info.m_ticketPrice }, -1);
            }

            return ticketPriceDefault;
        }

        public static void RemoveAllUnwantedVehicles()
        {
            VehicleManager vm = Singleton<VehicleManager>.instance;
            for (ushort lineId = 1; lineId < Singleton<TransportManager>.instance.m_lines.m_size; lineId++)
            {
                if ((Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].m_flags & TransportLine.Flags.Created) != TransportLine.Flags.None)
                {
                    IBasicExtension extension = TLMLineUtils.GetEffectiveExtensionForLine(lineId);
                    ref TransportLine tl = ref Singleton<TransportManager>.instance.m_lines.m_buffer[lineId];
                    List<string> modelList = extension.GetAssetListForLine(lineId);

                    if (TransportLinesManagerMod.DebugMode)
                    {
                        TLMUtils.doLog("removeAllUnwantedVehicles: models found: {0}", modelList == null ? "?!?" : modelList.Count.ToString());
                    }

                    if (modelList.Count > 0)
                    {
                        var vehiclesToRemove = new Dictionary<ushort, VehicleInfo>();
                        for (int i = 0; i < tl.CountVehicles(lineId); i++)
                        {
                            ushort vehicle = tl.GetVehicle(i);
                            if (vehicle != 0)
                            {
                                VehicleInfo info2 = vm.m_vehicles.m_buffer[vehicle].Info;
                                if (!modelList.Contains(info2.name))
                                {
                                    vehiclesToRemove[vehicle] = info2;
                                }
                            }
                        }
                        foreach (KeyValuePair<ushort, VehicleInfo> item in vehiclesToRemove)
                        {
                            if (item.Value.m_vehicleAI is BusAI)
                            {
                                VehicleUtils.ReplaceVehicleModel(item.Key, extension.GetAModel(lineId));
                            }
                            else
                            {
                                item.Value.m_vehicleAI.SetTransportLine(item.Key, ref vm.m_vehicles.m_buffer[item.Key], 0);
                            }
                        }
                    }
                }
            }
        }

        //public static IEnumerator RespawnVehicleInfoInLines(VehicleInfo info)
        //{
        //    yield return 0;
        //    VehicleManager vm = Singleton<VehicleManager>.instance;
        //    for (ushort lineId = 1; lineId < Singleton<TransportManager>.instance.m_lines.m_size; lineId++)
        //    {
        //        if ((Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].m_flags & TransportLine.Flags.Created) != TransportLine.Flags.None)
        //        {
        //            for (int i = 0; i < Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].CountVehicles(lineId); i++)
        //            {
        //                ushort vehicle = Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].GetVehicle(i);
        //                if (vehicle != 0)
        //                {
        //                    if (vm.m_vehicles.m_buffer[vehicle].Info.name == info.name)
        //                    {
        //                        if (info.m_vehicleAI is BusAI)
        //                        {
        //                            VehicleUtils.ReplaceVehicleModel(vehicle, info);
        //                        }
        //                        else
        //                        {
        //                            new EnumerableActionThread(new Func<ThreadBase, IEnumerator>(UpdateCapacityUnits));
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    yield break;
        //}
        private static int GetTotalUnitGroups(uint unitID)
        {
            int num = 0;
            while (unitID != 0u)
            {
                CitizenUnit citizenUnit = Singleton<CitizenManager>.instance.m_units.m_buffer[(int)((UIntPtr)unitID)];
                unitID = citizenUnit.m_nextUnit;
                num++;
            }
            return num;
        }
        public static IEnumerator UpdateCapacityUnits()
        {
            int count = 0;
            Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;
            int i = 0;
            TransportSystemDefinition tsd;
            ITLMTransportTypeExtension ext;
            while (i < (long)((ulong)vehicles.m_size))
            {
                if ((vehicles.m_buffer[i].m_flags & Vehicle.Flags.Spawned) == Vehicle.Flags.Spawned && (tsd = TransportSystemDefinition.From(vehicles.m_buffer[i].Info)) != default && (ext = tsd.GetTransportExtension()).IsCustomCapacity(vehicles.m_buffer[i].Info.name))
                {
                    int capacity = ext.GetCustomCapacity(vehicles.m_buffer[i].Info.name);
                    if (capacity != -1)
                    {
                        CitizenUnit[] units = Singleton<CitizenManager>.instance.m_units.m_buffer;
                        uint unit = vehicles.m_buffer[i].m_citizenUnits;
                        int currentUnitCount = GetTotalUnitGroups(unit);
                        int newUnitCount = Mathf.CeilToInt(capacity / 5f);
                        if (newUnitCount < currentUnitCount)
                        {
                            uint j = unit;
                            for (int k = 1; k < newUnitCount; k++)
                            {
                                j = units[(int)((UIntPtr)j)].m_nextUnit;
                            }
                            Singleton<CitizenManager>.instance.ReleaseUnits(units[(int)((UIntPtr)j)].m_nextUnit);
                            units[(int)((UIntPtr)j)].m_nextUnit = 0u;
                            count++;
                        }
                        else if (newUnitCount > currentUnitCount)
                        {
                            uint l = unit;
                            while (units[(int)((UIntPtr)l)].m_nextUnit != 0u)
                            {
                                l = units[(int)((UIntPtr)l)].m_nextUnit;
                            }
                            int newCapacity = capacity - currentUnitCount * 5;
                            if (!Singleton<CitizenManager>.instance.CreateUnits(out units[l].m_nextUnit, ref Singleton<SimulationManager>.instance.m_randomizer, 0, (ushort)i, 0, 0, 0, newCapacity, 0))
                            {
                                LogUtils.DoErrorLog("FAILED CREATING UNITS!!!!");
                            }
                            count++;
                        }
                    }
                }
                if (i % 256 == 255)
                {
                    yield return i % 256;
                }
                i++;
            }
            yield break;
        }


        internal static readonly ModoNomenclatura[] nomenclaturasComNumeros = new ModoNomenclatura[]
        {
        ModoNomenclatura. LatinoMinusculoNumero ,
        ModoNomenclatura. LatinoMaiusculoNumero ,
        ModoNomenclatura. GregoMinusculoNumero,
        ModoNomenclatura. GregoMaiusculoNumero,
        ModoNomenclatura. CirilicoMinusculoNumero,
        ModoNomenclatura. CirilicoMaiusculoNumero
        };

        #endregion
    }

    public enum NamingType
    {
        NONE,
        PLANE,
        BLIMP,
        SHIP,
        FERRY,
        TRAIN,
        MONORAIL,
        TRAM,
        METRO,
        BUS,
        TOUR_BUS,
        CABLE_CAR,
        MONUMENT,
        BEAUTIFICATION,
        HEALTHCARE,
        POLICEDEPARTMENT,
        FIREDEPARTMENT,
        EDUCATION,
        DISASTER,
        GARBAGE,
        CAMPUS,
        PARKAREA,
        INDUSTRY_AREA,
        DISTRICT,
        ADDRESS,
        RICO,
        TROLLEY,
        HELICOPTER
    }

    internal static class NamingTypeExtensions
    {
        public static int GetNamePrecedenceRate(this NamingType namingType)
        {
            switch (namingType)
            {
                case NamingType.NONE:
                    return 0x7FFFFFFF;
                case NamingType.PLANE:
                    return -0x00000005;
                case NamingType.BLIMP:
                    return 0x00000001;
                case NamingType.SHIP:
                    return -0x00000002;
                case NamingType.FERRY:
                    return 0x00000001;
                case NamingType.TRAIN:
                    return 0x00000003;
                case NamingType.MONORAIL:
                    return 0x00000004;
                case NamingType.TRAM:
                    return 0x00000006;
                case NamingType.METRO:
                    return 0x00000005;
                case NamingType.BUS:
                    return 0x00000007;
                case NamingType.TOUR_BUS:
                    return 0x00000009;
                case NamingType.MONUMENT:
                    return 0x00000005;
                case NamingType.CAMPUS:
                    return 0x00000005;
                case NamingType.BEAUTIFICATION:
                    return 0x0000000a;
                case NamingType.HEALTHCARE:
                    return 0x0000000b;
                case NamingType.POLICEDEPARTMENT:
                    return 0x0000000b;
                case NamingType.FIREDEPARTMENT:
                    return 0x0000000b;
                case NamingType.EDUCATION:
                    return 0x0000000c;
                case NamingType.DISASTER:
                    return 0x0000000d;
                case NamingType.GARBAGE:
                    return 0x0000000f;
                case NamingType.PARKAREA:
                    return 0x00000005;
                case NamingType.DISTRICT:
                    return 0x00000010;
                case NamingType.INDUSTRY_AREA:
                    return 0x00000010;
                case NamingType.ADDRESS:
                    return 0x00000011;
                case NamingType.RICO:
                    return 0x000000e;
                case NamingType.CABLE_CAR:
                    return 0x00000004;
                case NamingType.TROLLEY:
                    return 0x00000006;
                case NamingType.HELICOPTER:
                    return 0x00000001;
                default:
                    return 0x7FFFFFFF;
            }
        }

        public static NamingType From(ItemClass.Service service, ItemClass.SubService subService) => From(GameServiceExtensions.ToConfigIndex(service, subService));
        public static NamingType From(TLMCW.ConfigIndex ci)
        {
            switch (ci & (TLMCW.ConfigIndex.SYSTEM_PART | TLMCW.ConfigIndex.DESC_DATA))
            {
                case TLMCW.ConfigIndex.PLANE_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.PLANE;
                case TLMCW.ConfigIndex.BLIMP_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.BLIMP;
                case TLMCW.ConfigIndex.SHIP_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.SHIP;
                case TLMCW.ConfigIndex.FERRY_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.FERRY;
                case TLMCW.ConfigIndex.TRAIN_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.TRAIN;
                case TLMCW.ConfigIndex.MONORAIL_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.MONORAIL;
                case TLMCW.ConfigIndex.TRAM_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.TRAM;
                case TLMCW.ConfigIndex.METRO_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.METRO;
                case TLMCW.ConfigIndex.BUS_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.BUS;
                case TLMCW.ConfigIndex.TOUR_BUS_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.TOUR_BUS;
                case TLMCW.ConfigIndex.CABLE_CAR_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.CABLE_CAR;
                case TLMCW.ConfigIndex.MONUMENT_SERVICE_CONFIG:
                    return NamingType.MONUMENT;
                case TLMCW.ConfigIndex.BEAUTIFICATION_SERVICE_CONFIG:
                    return NamingType.BEAUTIFICATION;
                case TLMCW.ConfigIndex.HEALTHCARE_SERVICE_CONFIG:
                    return NamingType.HEALTHCARE;
                case TLMCW.ConfigIndex.POLICEDEPARTMENT_SERVICE_CONFIG:
                    return NamingType.POLICEDEPARTMENT;
                case TLMCW.ConfigIndex.FIREDEPARTMENT_SERVICE_CONFIG:
                    return NamingType.FIREDEPARTMENT;
                case TLMCW.ConfigIndex.EDUCATION_SERVICE_CONFIG:
                    return NamingType.EDUCATION;
                case TLMCW.ConfigIndex.DISASTER_SERVICE_CONFIG:
                    return NamingType.DISASTER;
                case TLMCW.ConfigIndex.GARBAGE_SERVICE_CONFIG:
                    return NamingType.GARBAGE;
                case TLMCW.ConfigIndex.PARKAREA_NAME_CONFIG:
                    return NamingType.PARKAREA;
                case TLMCW.ConfigIndex.DISTRICT_NAME_CONFIG:
                    return NamingType.DISTRICT;
                case TLMCW.ConfigIndex.ADDRESS_NAME_CONFIG:
                    return NamingType.ADDRESS;
                case TLMCW.ConfigIndex.VARSITY_SPORTS_SERVICE_CONFIG:
                case TLMCW.ConfigIndex.MUSEUMS_SERVICE_CONFIG:
                case TLMCW.ConfigIndex.PLAYER_EDUCATION_SERVICE_CONFIG:
                    return NamingType.CAMPUS;
                case TLMCW.ConfigIndex.PLAYER_INDUSTRY_SERVICE_CONFIG:
                    return NamingType.INDUSTRY_AREA;
                case TLMCW.ConfigIndex.RESIDENTIAL_SERVICE_CONFIG:
                case TLMCW.ConfigIndex.INDUSTRIAL_SERVICE_CONFIG:
                case TLMCW.ConfigIndex.COMMERCIAL_SERVICE_CONFIG:
                case TLMCW.ConfigIndex.OFFICE_SERVICE_CONFIG:
                    return NamingType.RICO;
                case TLMCW.ConfigIndex.TROLLEY_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.TROLLEY;
                case TLMCW.ConfigIndex.HELICOPTER_CONFIG | TLMCW.ConfigIndex.PUBLICTRANSPORT_SERVICE_CONFIG:
                    return NamingType.HELICOPTER;
                default:
                    TLMUtils.doErrorLog($"UNKNOWN NAME TYPE:{ci} ({((int)ci).ToString("X8")})");
                    return NamingType.NONE;

            }
        }
    }

}

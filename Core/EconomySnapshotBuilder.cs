using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EconomyRevamp
{
    internal sealed class EconomySnapshotBuilder
    {
        private static readonly FieldInfo GoodsInWarehouseField =
            typeof(IslandMissionOffice).GetField("goodsInWarehouse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo WarehouseSlotField =
            typeof(IslandMissionOffice).GetField("slot", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo EconTimerField =
            typeof(IslandMarket).GetField("econTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        public string Build()
        {
            JsonResponseBuilder json = new JsonResponseBuilder();
            IslandMarket[] markets = UnityEngine.Object.FindObjectsOfType<IslandMarket>();
            Array.Sort(markets, CompareMarkets);

            json.BeginObject();
            json.Property("schemaVersion", 2);
            json.Property("status", markets.Length == 0 ? "waiting-for-markets" : "ok");
            json.Property("generatedAtUtc", DateTime.UtcNow.ToString("o"));
            json.Property("gameDay", SafeInt(delegate { return GameState.day; }, 0));
            json.Property("marketCount", markets.Length);

            WriteClock(json);
            WriteDebugMarketTracker(json);
            WriteCurrencyMarket(json);
            WriteGoodCatalog(json);
            WriteMarkets(json, markets);

            json.EndObject();
            return json.ToString();
        }

        private static int CompareMarkets(IslandMarket left, IslandMarket right)
        {
            int leftIndex = SafeInt(delegate { return left.GetPortIndex(); }, 9999);
            int rightIndex = SafeInt(delegate { return right.GetPortIndex(); }, 9999);
            return leftIndex.CompareTo(rightIndex);
        }

        private static void WriteDebugMarketTracker(JsonResponseBuilder json)
        {
            json.BeginObjectProperty("debugMarketTracker");
            DebugMarketTracker tracker = DebugMarketTracker.instance;
            json.Property("available", tracker != null);
            json.Property("marketSpeedMult", DebugMarketTracker.marketSpeedMult);
            json.Property("marketProductionMult", DebugMarketTracker.marketProductionMult);
            json.Property("goodsAmountSoftCap", DebugMarketTracker.goodsAmountSoftCap);
            json.Property("priceDiffFixedMult", DebugMarketTracker.priceDiffFixedMult);
            json.Property("priceDiffValueMult", DebugMarketTracker.priceDiffValueMult);
            if (tracker != null)
            {
                json.Property("finalMarketSpeed", tracker.finalMarketSpeed);
                json.Property("missionProfitShareLocal", tracker.missionProfitShareLocal);
                json.Property("missionProfitShareWorld", tracker.missionProfitShareWorld);
                json.Property("missionDistanceFee", tracker.missionDistanceFee);
                json.Property("missionFinalMult", tracker.missionFinalMult);
                json.Property("negativePriceMult", tracker.negativePriceMult);
                json.Property("positivePriceMult", tracker.positivePriceMult);
            }
            json.EndObject();
        }

        private static void WriteClock(JsonResponseBuilder json)
        {
            Sun sun = Sun.sun;
            float timescale = sun == null ? 0f : sun.timescale;
            float marketSpeedMult = DebugMarketTracker.marketSpeedMult;
            float typicalDuration = marketSpeedMult > 0f ? 0.2f / marketSpeedMult : float.NaN;

            json.BeginObjectProperty("clock");
            json.Property("sunAvailable", sun != null);
            json.Property("sunPaused", SafeBool(Sun.SunPaused, false));
            json.Property("sunTimescale", timescale);
            json.Property("initialSunTimescale", sun == null ? 0f : sun.initialTimescale);
            json.Property("globalTime", sun == null ? 0f : sun.globalTime);
            json.Property("localTime", sun == null ? 0f : sun.localTime);
            json.Property("marketSpeedMult", marketSpeedMult);
            json.Property("marketTimerScalePerRealSecond", timescale * 100f);
            json.Property("typicalCycleTimerUnits", typicalDuration);
            json.Property("typicalCycleGameHours", typicalDuration / 100f);
            json.Property("typicalCycleGameSeconds", typicalDuration / 100f * 3600f);
            json.Property("typicalCycleRealSeconds", timescale > 0f ? typicalDuration / (timescale * 100f) : float.NaN);
            json.EndObject();
        }

        private static void WriteCurrencyMarket(JsonResponseBuilder json)
        {
            json.BeginObjectProperty("currencyMarket");
            CurrencyMarket market = CurrencyMarket.instance;
            json.Property("available", market != null);
            if (market != null)
            {
                json.Property("changeFactor", market.changeFactor);
                json.Property("dailyChangeFactor", market.dailyChangeFactor);
                json.Property("exchangeFee", SafeFloat(delegate { return market.GetExchangeFee(); }, 0f));
                json.BeginArrayProperty("currentPrices");
                WriteFloatArrayValues(json, market.currentPrices);
                json.EndArray();
            }
            json.EndObject();
        }

        private static void WriteGoodCatalog(JsonResponseBuilder json)
        {
            int count = GetGoodCount();
            json.BeginArrayProperty("goodsCatalog");
            for (int i = 0; i < count; i++)
            {
                WriteGoodCatalogItem(json, i);
            }
            json.EndArray();
        }

        private static void WriteGoodCatalogItem(JsonResponseBuilder json, int goodIndex)
        {
            ShipItem item = GetGood(goodIndex);
            Good good = item == null ? null : item.GetComponent<Good>();

            json.BeginObject();
            json.Property("goodIndex", goodIndex);
            json.Property("itemIndex", SafeInt(delegate { return PrefabsDirectory.GoodToItemIndex(goodIndex); }, goodIndex));
            json.Property("available", item != null);
            json.Property("name", GetGoodName(goodIndex));
            json.Property("category", item == null ? string.Empty : item.category.ToString());
            json.Property("value", item == null ? 0 : item.value);
            json.Property("mass", item == null ? 0f : item.mass);
            json.Property("nativeRegion", good == null ? string.Empty : good.nativeRegion.ToString());
            json.Property("requiredRepLevel", good == null ? 0 : good.requiredRepLevel);
            json.Property("sizeDescription", good == null ? string.Empty : good.sizeDescription);
            json.EndObject();
        }

        private static void WriteMarkets(JsonResponseBuilder json, IslandMarket[] markets)
        {
            json.BeginArrayProperty("markets");
            for (int i = 0; i < markets.Length; i++)
            {
                WriteMarket(json, markets[i]);
            }
            json.EndArray();
        }

        private static void WriteMarket(JsonResponseBuilder json, IslandMarket market)
        {
            Port port = SafeObject(delegate { return market.GetPort(); });
            Vector3 position = market.transform.position;
            float econTimer = ReadEconTimer(market);
            float resetTimer = DebugMarketTracker.marketSpeedMult > 0f
                ? market.econCycleDuration / DebugMarketTracker.marketSpeedMult
                : float.NaN;
            float remainingTimer = Mathf.Max(0f, econTimer);
            float sunTimescale = Sun.sun == null ? 0f : Sun.sun.timescale;

            json.BeginObject();
            json.Property("portIndex", SafeInt(delegate { return market.GetPortIndex(); }, -1));
            json.Property("portName", SafeString(delegate { return market.GetPortName(); }, market.name));
            json.Property("objectName", market.name);
            json.Property("region", port == null ? SafeString(delegate { return market.GetPortRegion().ToString(); }, string.Empty) : port.region.ToString());
            json.Property("supplyPurchaseLimit", market.supplyPurchaseLimit);
            json.Property("pricePerGoodMult", market.pricePerGoodMult);
            json.Property("goodsSoftCapOverride", market.goodsSoftCapOverride);
            json.Property("allowCurrencyConversion", market.allowCurrencyConversion);
            json.Property("wealthMult", market.wealthMult);
            json.Property("econCycleDuration", market.econCycleDuration);
            json.Property("econTimerAvailable", EconTimerField != null);
            json.Property("econTimer", econTimer);
            json.Property("econTimerResetValue", resetTimer);
            json.Property("cycleProgress", resetTimer > 0f ? Mathf.Clamp01(1f - remainingTimer / resetTimer) : float.NaN);
            json.Property("gameHoursToNextCycle", remainingTimer / 100f);
            json.Property("gameSecondsToNextCycle", remainingTimer / 100f * 3600f);
            json.Property("realSecondsToNextCycle", sunTimescale > 0f ? remainingTimer / (sunTimescale * 100f) : float.NaN);
            json.Property("debugTotalGoodsTraded", market.debugTotalGoodsTraded);
            json.Property("debugTraderVisits", market.debugTraderVisits);

            json.BeginObjectProperty("position");
            json.Property("x", position.x);
            json.Property("y", position.y);
            json.Property("z", position.z);
            json.EndObject();

            WriteDestinationPorts(json, port);
            WriteWarehouse(json, market);
            WriteMarketGoods(json, market);
            WriteKnownPriceReports(json, market);

            json.EndObject();
        }

        private static float ReadEconTimer(IslandMarket market)
        {
            if (EconTimerField == null)
            {
                return float.NaN;
            }

            return SafeFloat(delegate { return (float)EconTimerField.GetValue(market); }, float.NaN);
        }

        private static void WriteDestinationPorts(JsonResponseBuilder json, Port port)
        {
            json.BeginArrayProperty("destinationPortIndexes");
            if (port != null)
            {
                Port[] destinations = SafeObject(delegate { return port.GetDestinationPorts(); });
                if (destinations != null)
                {
                    for (int i = 0; i < destinations.Length; i++)
                    {
                        if (destinations[i] != null)
                        {
                            json.Value(destinations[i].portIndex);
                        }
                    }
                }
            }
            json.EndArray();
        }

        private static void WriteWarehouse(JsonResponseBuilder json, IslandMarket market)
        {
            IslandMissionOffice office = market.GetComponent<IslandMissionOffice>();
            int[] slots = null;
            int cursor = 0;
            if (office != null && GoodsInWarehouseField != null)
            {
                slots = GoodsInWarehouseField.GetValue(office) as int[];
            }
            if (office != null && WarehouseSlotField != null)
            {
                cursor = SafeInt(delegate { return (int)WarehouseSlotField.GetValue(office); }, 0);
            }

            json.BeginObjectProperty("warehouse");
            json.Property("available", slots != null);
            json.Property("cursor", cursor);
            json.Property("capacity", slots == null ? 0 : slots.Length);

            json.BeginArrayProperty("slots");
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    json.Value(slots[i]);
                }
            }
            json.EndArray();

            json.BeginArrayProperty("counts");
            if (slots != null)
            {
                Dictionary<int, int> counts = new Dictionary<int, int>();
                for (int i = 0; i < slots.Length; i++)
                {
                    int goodIndex = slots[i];
                    if (!counts.ContainsKey(goodIndex))
                    {
                        counts[goodIndex] = 0;
                    }
                    counts[goodIndex]++;
                }

                List<int> keys = new List<int>(counts.Keys);
                keys.Sort();
                for (int i = 0; i < keys.Count; i++)
                {
                    int goodIndex = keys[i];
                    json.BeginObject();
                    json.Property("goodIndex", goodIndex);
                    json.Property("name", GetGoodName(goodIndex));
                    json.Property("count", counts[goodIndex]);
                    json.EndObject();
                }
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteMarketGoods(JsonResponseBuilder json, IslandMarket market)
        {
            int count = Math.Max(GetLength(market.production), GetLength(market.currentSupply));
            count = Math.Max(count, GetLength(market.currentPlayerGoods));
            json.BeginArrayProperty("goods");
            for (int i = 0; i < count; i++)
            {
                ShipItem item = GetGood(i);
                Good good = item == null ? null : item.GetComponent<Good>();
                float supply = GetFloatAt(market.currentSupply, i);
                float production = GetFloatAt(market.production, i);
                float softCap = GetEffectiveSoftCap(market);
                float cycleCapFactor = GetCycleCapFactor(softCap, supply);
                float productionMultiplier = production > 0f ? DebugMarketTracker.marketProductionMult : 1f;
                float expectedCycleDelta = production * 0.21f * cycleCapFactor * productionMultiplier;

                json.BeginObject();
                json.Property("goodIndex", i);
                json.Property("itemIndex", SafeInt(delegate { return PrefabsDirectory.GoodToItemIndex(i); }, i));
                json.Property("name", GetGoodName(i));
                json.Property("category", item == null ? string.Empty : item.category.ToString());
                json.Property("nativeRegion", good == null ? string.Empty : good.nativeRegion.ToString());
                json.Property("requiredRepLevel", good == null ? 0 : good.requiredRepLevel);
                json.Property("value", item == null ? 0 : item.value);
                json.Property("mass", item == null ? 0f : item.mass);
                json.Property("production", production);
                json.Property("currentSupply", supply);
                json.Property("effectiveSoftCap", softCap);
                json.Property("cycleCapFactor", cycleCapFactor);
                json.Property("productionMultiplier", productionMultiplier);
                json.Property("expectedCycleDelta", expectedCycleDelta);
                json.Property("currentPlayerGoods", GetIntAt(market.currentPlayerGoods, i));
                json.Property("hasGood", SafeBool(delegate { return market.HasGood(i); }, false));
                json.Property("basePrice", SafeInt(delegate { return market.GetGoodPrice(i); }, 0));
                json.Property("buyPrice", SafeInt(delegate { return market.GetBuyPrice(i); }, 0));
                json.Property("sellPrice", SafeInt(delegate { return market.GetSellPrice(i); }, 0));
                json.EndObject();
            }
            json.EndArray();
        }

        private static float GetEffectiveSoftCap(IslandMarket market)
        {
            if (market.goodsSoftCapOverride != 0f)
            {
                return market.goodsSoftCapOverride;
            }

            return DebugMarketTracker.goodsAmountSoftCap;
        }

        private static float GetCycleCapFactor(float softCap, float supply)
        {
            if (softCap <= 0f)
            {
                return 0f;
            }

            return Mathf.InverseLerp(softCap, 0f, Mathf.Abs(supply));
        }

        private static void WriteKnownPriceReports(JsonResponseBuilder json, IslandMarket market)
        {
            json.BeginArrayProperty("knownPriceReports");
            PriceReport[] reports = market.knownPrices;
            if (reports != null)
            {
                for (int i = 0; i < reports.Length; i++)
                {
                    PriceReport report = reports[i];
                    if (report == null || !report.approved)
                    {
                        continue;
                    }

                    json.BeginObject();
                    json.Property("portIndex", i);
                    json.Property("day", report.day);
                    json.BeginArrayProperty("buyPrices");
                    WriteIntArrayValues(json, report.buyPrices);
                    json.EndArray();
                    json.BeginArrayProperty("sellPrices");
                    WriteIntArrayValues(json, report.sellPrices);
                    json.EndArray();
                    json.EndObject();
                }
            }
            json.EndArray();
        }

        private static int GetGoodCount()
        {
            int count = 65;
            if (PrefabsDirectory.instance != null && PrefabsDirectory.instance.shipItems != null)
            {
                count = Math.Max(count, PrefabsDirectory.ItemToGoodIndex(PrefabsDirectory.instance.shipItems.Length - 1) + 1);
            }
            return count;
        }

        private static ShipItem GetGood(int goodIndex)
        {
            return SafeObject(delegate
            {
                if (PrefabsDirectory.instance == null)
                {
                    return null;
                }

                return PrefabsDirectory.instance.GetGood(goodIndex);
            });
        }

        private static string GetGoodName(int goodIndex)
        {
            if (goodIndex == 0)
            {
                return "Empty";
            }

            ShipItem item = GetGood(goodIndex);
            if (item == null)
            {
                return "Good " + goodIndex;
            }

            if (!string.IsNullOrEmpty(item.name))
            {
                return item.name;
            }

            return item.gameObject == null ? "Good " + goodIndex : item.gameObject.name;
        }

        private static void WriteIntArrayValues(JsonResponseBuilder json, int[] values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                json.Value(values[i]);
            }
        }

        private static void WriteFloatArrayValues(JsonResponseBuilder json, float[] values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                json.Value(values[i]);
            }
        }

        private static int GetLength(Array array)
        {
            return array == null ? 0 : array.Length;
        }

        private static float GetFloatAt(float[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
        }

        private static int GetIntAt(int[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        private static int SafeInt(Func<int> read, int fallback)
        {
            try
            {
                return read();
            }
            catch
            {
                return fallback;
            }
        }

        private static float SafeFloat(Func<float> read, float fallback)
        {
            try
            {
                return read();
            }
            catch
            {
                return fallback;
            }
        }

        private static bool SafeBool(Func<bool> read, bool fallback)
        {
            try
            {
                return read();
            }
            catch
            {
                return fallback;
            }
        }

        private static string SafeString(Func<string> read, string fallback)
        {
            try
            {
                string value = read();
                return value ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static T SafeObject<T>(Func<T> read) where T : class
        {
            try
            {
                return read();
            }
            catch
            {
                return null;
            }
        }
    }
}

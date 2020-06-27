﻿using Klyte.Commons.Interfaces;
using Klyte.Commons.Utils;
using System;
using System.IO;

namespace Klyte.TransportLinesManager.Extensors
{
    public partial class TLMTransportLineStatusesManager
    {
        public abstract class TransportLineStorageBasicData : IDataExtensor
        {
            public abstract string SaveId { get; }

            protected abstract Enum[] LoadOrder { get; }

            public IDataExtensor Deserialize(Type type, byte[] rawData)
            {

                byte[] data;
                try
                {
                    data = ZipUtils.UnzipBytes(rawData);
                }
                catch
                {
                    LogUtils.DoWarnLog("NOTE: Data is not zipped!");
                    data = rawData;
                }


                using var s = new MemoryStream(data);
                long version = ReadLong(s);
                foreach (Enum e in LoadOrder)
                {
                    if (version >= GetMinVersion(e))
                    {

                        TLMTransportLineStatusesManager.instance.DoWithArray(e, (ref long[][] arrayRef) =>
                        {
                            int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);

                            for (int i = 0; i < arrayRef.Length; i++)
                            {
                                arrayRef[i][idx] = DeserializeFunction(s);
                            }
                        }, (ref int[][] arrayRef) =>
                        {
                            int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);

                            for (int i = 0; i < arrayRef.Length; i++)
                            {
                                arrayRef[i][idx] = (int)DeserializeFunction(s);
                            }
                        }, (ref ushort[][] arrayRef) =>
                        {
                            int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);

                            for (int i = 0; i < arrayRef.Length; i++)
                            {
                                arrayRef[i][idx] = (ushort)DeserializeFunction(s);
                            }
                        });
                    }
                }
                return this;
            }

            public byte[] Serialize()
            {
                using var s = new MemoryStream();

                WriteLong(s, CURRENT_VERSION);


                foreach (Enum e in LoadOrder)
                {
                    TLMTransportLineStatusesManager.instance.DoWithArray(e, (ref long[][] arrayRef) =>
                    {
                        int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);
                        for (int i = 0; i < arrayRef.Length; i++)
                        {
                            SerializeFunction(s, arrayRef[i][idx]);
                        }
                        LogUtils.DoWarnLog($"size: {s.Length} ({e.GetType()} {e})");
                    }, (ref int[][] arrayRef) =>
                    {
                        int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);
                        for (int i = 0; i < arrayRef.Length; i++)
                        {
                            SerializeFunction(s, arrayRef[i][idx]);
                        }
                        LogUtils.DoWarnLog($"size: {s.Length} ({e.GetType()} {e})");
                    }, (ref ushort[][] arrayRef) =>
                    {
                        int idx = TLMTransportLineStatusesManager.instance.GetIdxFor(e);
                        for (int i = 0; i < arrayRef.Length; i++)
                        {
                            SerializeFunction(s, arrayRef[i][idx]);
                        }
                        LogUtils.DoWarnLog($"size: {s.Length} ({e.GetType()} {e})");
                    });

                }
                return ZipUtils.ZipBytes(s.ToArray());
            }
            protected static void WriteLong(Stream s, long value)
            {
                s.WriteByte((byte)((value >> 56) & 255L));
                s.WriteByte((byte)((value >> 48) & 255L));
                s.WriteByte((byte)((value >> 40) & 255L));
                s.WriteByte((byte)((value >> 32) & 255L));
                s.WriteByte((byte)((value >> 24) & 255L));
                s.WriteByte((byte)((value >> 16) & 255L));
                s.WriteByte((byte)((value >> 8) & 255L));
                s.WriteByte((byte)(value & 255L));
            }

            protected static long ReadLong(Stream s)
            {
                long num = (long)(s.ReadByte() & 255) << 56;
                num |= (long)(s.ReadByte() & 255) << 48;
                num |= (long)(s.ReadByte() & 255) << 40;
                num |= (long)(s.ReadByte() & 255) << 32;
                num |= (long)(s.ReadByte() & 255) << 24;
                num |= (long)(s.ReadByte() & 255) << 16;
                num |= (long)(s.ReadByte() & 255) << 8;
                return num | (s.ReadByte() & 255 & 255L);
            }
            protected static void WriteInt32(Stream s, long value)
            {
                s.WriteByte((byte)((value >> 24) & 255L));
                s.WriteByte((byte)((value >> 16) & 255L));
                s.WriteByte((byte)((value >> 8) & 255L));
                s.WriteByte((byte)(value & 255L));
            }

            protected static long ReadInt32(Stream s)
            {
                long num = (long)(s.ReadByte() & 255) << 24;
                num |= (long)(s.ReadByte() & 255) << 16;
                num |= (long)(s.ReadByte() & 255) << 8;
                return num | (s.ReadByte() & 255 & 255L);
            }
            protected static void WriteInt24(Stream s, long value)
            {
                s.WriteByte((byte)((value >> 16) & 255L));
                s.WriteByte((byte)((value >> 8) & 255L));
                s.WriteByte((byte)(value & 255L));
            }

            protected static long ReadInt24(Stream s)
            {
                long num = (long)(s.ReadByte() & 255) << 16;
                num |= (long)(s.ReadByte() & 255) << 8;
                return num | (s.ReadByte() & 255 & 255L);
            }
            protected static void WriteInt16(Stream s, long value)
            {
                s.WriteByte((byte)((value >> 8) & 255L));
                s.WriteByte((byte)(value & 255L));
            }

            protected static long ReadInt16(Stream s)
            {
                long num = (long)(s.ReadByte() & 255) << 8;
                return num | (s.ReadByte() & 255 & 255L);
            }

            protected virtual Action<Stream, long> SerializeFunction { get; } = WriteLong;
            protected virtual Func<Stream, long> DeserializeFunction { get; } = ReadLong;

            public void OnReleased() { }

            public void LoadDefaults() { }
        }

        public class TLMTransportLineStorageEconomyData : TransportLineStorageBasicData
        {
            public override string SaveId => "K45_TLM_TLMTransportLineStorageEconomyData";

            protected override Enum[] LoadOrder { get; } = new Enum[]
                                                            {
                                                                LineDataLong.EXPENSE,
                                                                LineDataLong.INCOME,
                                                                StopDataLong.INCOME,
                                                                VehicleDataLong.EXPENSE,
                                                                VehicleDataLong.INCOME,
                                                            };
        }
        public class TLMTransportLineStoragePassengerData : TransportLineStorageBasicData
        {
            public override string SaveId => "K45_TLM_TLMTransportLineStoragePassengerData";

            protected override Enum[] LoadOrder { get; } = new Enum[]
                                                            {
                                                                 VehicleDataInt.TOTAL_PASSENGERS,
                                                                 VehicleDataInt.TOURIST_PASSENGERS,
                                                                 VehicleDataInt.STUDENT_PASSENGERS,
                                                                 StopDataInt.TOTAL_PASSENGERS,
                                                                 StopDataInt.TOURIST_PASSENGERS,
                                                                 StopDataInt.STUDENT_PASSENGERS,
                                                                 LineDataInt.TOTAL_PASSENGERS,
                                                                 LineDataInt.TOURIST_PASSENGERS,
                                                                 LineDataInt.STUDENT_PASSENGERS
                                                            };
            protected override Action<Stream, long> SerializeFunction { get; } = WriteInt24;
            protected override Func<Stream, long> DeserializeFunction { get; } = ReadInt24;
        }
        public class TLMTransportLineStorageDetailedPassengerData : TransportLineStorageBasicData
        {
            public override string SaveId => "K45_TLM_TLMTransportLineStorageDetailedPassengerData";

            protected override Enum[] LoadOrder { get; } = new Enum[]
                                                            {
                                                              LineDataUshort.W1_CHILD_MALE_PASSENGERS,
                                                              LineDataUshort.W1_TEENS_MALE_PASSENGERS,
                                                              LineDataUshort.W1_YOUNG_MALE_PASSENGERS,
                                                              LineDataUshort.W1_ADULT_MALE_PASSENGERS,
                                                              LineDataUshort.W1_ELDER_MALE_PASSENGERS,
                                                              LineDataUshort.W2_CHILD_MALE_PASSENGERS,
                                                              LineDataUshort.W2_TEENS_MALE_PASSENGERS,
                                                              LineDataUshort.W2_YOUNG_MALE_PASSENGERS,
                                                              LineDataUshort.W2_ADULT_MALE_PASSENGERS,
                                                              LineDataUshort.W2_ELDER_MALE_PASSENGERS,
                                                              LineDataUshort.W3_CHILD_MALE_PASSENGERS,
                                                              LineDataUshort.W3_TEENS_MALE_PASSENGERS,
                                                              LineDataUshort.W3_YOUNG_MALE_PASSENGERS,
                                                              LineDataUshort.W3_ADULT_MALE_PASSENGERS,
                                                              LineDataUshort.W3_ELDER_MALE_PASSENGERS,
                                                              LineDataUshort.W1_CHILD_FEML_PASSENGERS,
                                                              LineDataUshort.W1_TEENS_FEML_PASSENGERS,
                                                              LineDataUshort.W1_YOUNG_FEML_PASSENGERS,
                                                              LineDataUshort.W1_ADULT_FEML_PASSENGERS,
                                                              LineDataUshort.W1_ELDER_FEML_PASSENGERS,
                                                              LineDataUshort.W2_CHILD_FEML_PASSENGERS,
                                                              LineDataUshort.W2_TEENS_FEML_PASSENGERS,
                                                              LineDataUshort.W2_YOUNG_FEML_PASSENGERS,
                                                              LineDataUshort.W2_ADULT_FEML_PASSENGERS,
                                                              LineDataUshort.W2_ELDER_FEML_PASSENGERS,
                                                              LineDataUshort.W3_CHILD_FEML_PASSENGERS,
                                                              LineDataUshort.W3_TEENS_FEML_PASSENGERS,
                                                              LineDataUshort.W3_YOUNG_FEML_PASSENGERS,
                                                              LineDataUshort.W3_ADULT_FEML_PASSENGERS,
                                                              LineDataUshort.W3_ELDER_FEML_PASSENGERS,
                                                            };
            protected override Action<Stream, long> SerializeFunction { get; } = WriteInt16;
            protected override Func<Stream, long> DeserializeFunction { get; } = ReadInt16;
        }
    }
}
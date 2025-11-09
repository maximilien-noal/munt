/* Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
 * Copyright (C) 2011-2022 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 2.1 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;

namespace MT32Emu;

using Bit8u = System.Byte;
using Bit32u = System.UInt32;

// Defines vital info about ROM file to be used by synth and applications

public class ROMInfo
{
    public enum Type { PCM, Control, Reverb }
    
    public enum PairType
    {
        // Complete ROM image ready to use with Synth.
        Full,
        // ROM image contains data that occupies lower addresses. Needs pairing before use.
        FirstHalf,
        // ROM image contains data that occupies higher addresses. Needs pairing before use.
        SecondHalf,
        // ROM image contains data that occupies even addresses. Needs pairing before use.
        Mux0,
        // ROM image contains data that occupies odd addresses. Needs pairing before use.
        Mux1
    }

    public nuint fileSize;
    public string sha1Digest;
    public Type type;
    public string? shortName;
    public string? description;
    public PairType pairType;
    // null for Full images or a pointer to the corresponding other image for pairing.
    public ROMInfo? pairROMInfo;

    public ROMInfo(nuint fileSize, string sha1Digest, Type type, string? shortName, string? description, PairType pairType, ROMInfo? pairROMInfo)
    {
        this.fileSize = fileSize;
        this.sha1Digest = sha1Digest;
        this.type = type;
        this.shortName = shortName;
        this.description = description;
        this.pairType = pairType;
        this.pairROMInfo = pairROMInfo;
    }

    // Returns a ROMInfo struct by inspecting the size and the SHA1 hash of the file
    // among all the known ROMInfos.
    public static ROMInfo? GetROMInfo(IFile file)
    {
        return GetROMInfo(file, GetKnownROMInfoList());
    }

    // Returns a ROMInfo struct by inspecting the size and the SHA1 hash of the file
    // among the ROMInfos listed in the null-terminated list romInfos.
    public static ROMInfo? GetROMInfo(IFile file, ROMInfo?[] romInfos)
    {
        nuint fileSize = file.GetSize();
        for (Bit32u i = 0; i < romInfos.Length && romInfos[i] != null; i++)
        {
            ROMInfo? romInfo = romInfos[i];
            if (romInfo != null && fileSize == romInfo.fileSize && file.GetSHA1() == romInfo.sha1Digest)
            {
                return romInfo;
            }
        }
        return null;
    }

    // Currently no-op
    public static void FreeROMInfo(ROMInfo? romInfo)
    {
        // No-op in C# (garbage collected)
    }

    // Allows retrieving a list of ROMInfos for a range of types and pairTypes
    // (specified by bitmasks)
    // Useful for GUI/console app to output information on what ROMs it supports
    public static ROMInfo?[] GetROMInfoList(Bit32u types, Bit32u pairTypes)
    {
        var romInfoLists = GetROMInfoLists();
        Bit32u romCount = romInfoLists.allROMInfos.itemCount;
        var romInfoList = new System.Collections.Generic.List<ROMInfo?>();
        
        for (Bit32u i = 0; i < romCount; i++)
        {
            ROMInfo? romInfo = GetKnownROMInfoFromList(i);
            if (romInfo != null && ((types & (1u << (int)romInfo.type)) != 0) && ((pairTypes & (1u << (int)romInfo.pairType)) != 0))
            {
                romInfoList.Add(romInfo);
            }
        }
        romInfoList.Add(null);
        return romInfoList.ToArray();
    }

    // Frees the list of ROMInfos given that has been created by getROMInfoList.
    public static void FreeROMInfoList(ROMInfo?[] romInfoList)
    {
        // No-op in C# (garbage collected)
    }

    // Returns an immutable list of all (full and partial) supported ROMInfos.
    public static ROMInfo?[] GetAllROMInfos(out Bit32u? itemCount)
    {
        var lists = GetROMInfoLists();
        itemCount = lists.allROMInfos.itemCount;
        return lists.allROMInfos.romInfos;
    }

    public static ROMInfo?[] GetAllROMInfos()
    {
        return GetROMInfoLists().allROMInfos.romInfos;
    }

    // Returns an immutable list of all supported full ROMInfos.
    public static ROMInfo?[] GetFullROMInfos(out Bit32u? itemCount)
    {
        var lists = GetROMInfoLists();
        itemCount = lists.fullROMInfos.itemCount;
        return lists.fullROMInfos.romInfos;
    }

    public static ROMInfo?[] GetFullROMInfos()
    {
        return GetROMInfoLists().fullROMInfos.romInfos;
    }

    // Returns an immutable list of all supported partial ROMInfos.
    public static ROMInfo?[] GetPartialROMInfos(out Bit32u? itemCount)
    {
        var lists = GetROMInfoLists();
        itemCount = lists.partialROMInfos.itemCount;
        return lists.partialROMInfos.romInfos;
    }

    public static ROMInfo?[] GetPartialROMInfos()
    {
        return GetROMInfoLists().partialROMInfos.romInfos;
    }

    internal class ROMInfoList
    {
        internal ROMInfo?[] romInfos;
        internal Bit32u itemCount;

        public ROMInfoList(ROMInfo?[] romInfos, Bit32u itemCount)
        {
            this.romInfos = romInfos;
            this.itemCount = itemCount;
        }
    }

    internal class ROMInfoLists
    {
        internal ROMInfoList mt32_1_04;
        internal ROMInfoList mt32_1_05;
        internal ROMInfoList mt32_1_06;
        internal ROMInfoList mt32_1_07;
        internal ROMInfoList mt32_bluer;
        internal ROMInfoList mt32_2_03;
        internal ROMInfoList mt32_2_04;
        internal ROMInfoList mt32_2_06;
        internal ROMInfoList mt32_2_07;
        internal ROMInfoList cm32l_1_00;
        internal ROMInfoList cm32l_1_02;
        internal ROMInfoList cm32ln_1_00;
        internal ROMInfoList fullROMInfos;
        internal ROMInfoList partialROMInfos;
        internal ROMInfoList allROMInfos;

        public ROMInfoLists(
            ROMInfoList mt32_1_04, ROMInfoList mt32_1_05, ROMInfoList mt32_1_06, ROMInfoList mt32_1_07, ROMInfoList mt32_bluer,
            ROMInfoList mt32_2_03, ROMInfoList mt32_2_04, ROMInfoList mt32_2_06, ROMInfoList mt32_2_07,
            ROMInfoList cm32l_1_00, ROMInfoList cm32l_1_02, ROMInfoList cm32ln_1_00,
            ROMInfoList fullROMInfos, ROMInfoList partialROMInfos, ROMInfoList allROMInfos)
        {
            this.mt32_1_04 = mt32_1_04;
            this.mt32_1_05 = mt32_1_05;
            this.mt32_1_06 = mt32_1_06;
            this.mt32_1_07 = mt32_1_07;
            this.mt32_bluer = mt32_bluer;
            this.mt32_2_03 = mt32_2_03;
            this.mt32_2_04 = mt32_2_04;
            this.mt32_2_06 = mt32_2_06;
            this.mt32_2_07 = mt32_2_07;
            this.cm32l_1_00 = cm32l_1_00;
            this.cm32l_1_02 = cm32l_1_02;
            this.cm32ln_1_00 = cm32ln_1_00;
            this.fullROMInfos = fullROMInfos;
            this.partialROMInfos = partialROMInfos;
            this.allROMInfos = allROMInfos;
        }
    }

    private static Bit32u CalcArrayLength(ROMInfo?[] array)
    {
        Bit32u count = 0;
        foreach (var item in array)
        {
            if (item == null) break;
            count++;
        }
        return count;
    }

    private static ROMInfoLists? s_romInfoLists;

    internal static ROMInfoLists GetROMInfoLists()
    {
        if (s_romInfoLists != null)
            return s_romInfoLists;

        // SHA1 Digests for Control ROMs
        const string CTRL_MT32_V1_04_A_SHA1 = "9cd4858014c4e8a9dff96053f784bfaac1092a2e";
        const string CTRL_MT32_V1_04_B_SHA1 = "fe8db469b5bfeb37edb269fd47e3ce6d91014652";
        const string CTRL_MT32_V1_04_SHA1 = "5a5cb5a77d7d55ee69657c2f870416daed52dea7";
        const string CTRL_MT32_V1_05_A_SHA1 = "57a09d80d2f7ca5b9734edbe9645e6e700f83701";
        const string CTRL_MT32_V1_05_B_SHA1 = "52e3c6666db9ef962591a8ee99be0cde17f3a6b6";
        const string CTRL_MT32_V1_05_SHA1 = "e17a3a6d265bf1fa150312061134293d2b58288c";
        const string CTRL_MT32_V1_06_A_SHA1 = "cc83bf23cee533097fb4c7e2c116e43b50ebacc8";
        const string CTRL_MT32_V1_06_B_SHA1 = "bf4f15666bc46679579498386704893b630c1171";
        const string CTRL_MT32_V1_06_SHA1 = "a553481f4e2794c10cfe597fef154eef0d8257de";
        const string CTRL_MT32_V1_07_A_SHA1 = "13f06b38f0d9e0fc050b6503ab777bb938603260";
        const string CTRL_MT32_V1_07_B_SHA1 = "c55e165487d71fa88bd8c5e9c083bc456c1a89aa";
        const string CTRL_MT32_V1_07_SHA1 = "b083518fffb7f66b03c23b7eb4f868e62dc5a987";
        const string CTRL_MT32_BLUER_A_SHA1 = "11a6ae5d8b6ee328b371af7f1e40b82125aa6b4d";
        const string CTRL_MT32_BLUER_B_SHA1 = "e0934320d7cbb5edfaa29e0d01ae835ef620085b";
        const string CTRL_MT32_BLUER_SHA1 = "7b8c2a5ddb42fd0732e2f22b3340dcf5360edf92";

        const string CTRL_MT32_V2_03_SHA1 = "5837064c9df4741a55f7c4d8787ac158dff2d3ce";
        const string CTRL_MT32_V2_04_SHA1 = "2c16432b6c73dd2a3947cba950a0f4c19d6180eb";
        const string CTRL_MT32_V2_06_SHA1 = "2869cf4c235d671668cfcb62415e2ce8323ad4ed";
        const string CTRL_MT32_V2_07_SHA1 = "47b52adefedaec475c925e54340e37673c11707c";
        const string CTRL_CM32L_V1_00_SHA1 = "73683d585cd6948cc19547942ca0e14a0319456d";
        const string CTRL_CM32L_V1_02_SHA1 = "a439fbb390da38cada95a7cbb1d6ca199cd66ef8";
        const string CTRL_CM32LN_V1_00_SHA1 = "dc1c5b1b90a4646d00f7daf3679733c7badc7077";

        // SHA1 Digests for PCM ROMs
        const string PCM_MT32_L_SHA1 = "3a1e19b0cd4036623fd1d1d11f5f25995585962b";
        const string PCM_MT32_H_SHA1 = "2cadb99d21a6a4a6f5b61b6218d16e9b43f61d01";
        const string PCM_MT32_SHA1 = "f6b1eebc4b2d200ec6d3d21d51325d5b48c60252";
        const string PCM_CM32L_H_SHA1 = "3ad889fde5db5b6437cbc2eb6e305312fec3df93";
        const string PCM_CM32L_SHA1 = "289cc298ad532b702461bfc738009d9ebe8025ea";

        // ROM Info objects - Control ROMs
        var CTRL_MT32_V1_04_A = new ROMInfo(32768, CTRL_MT32_V1_04_A_SHA1, Type.Control, "ctrl_mt32_1_04_a", "MT-32 Control v1.04", PairType.Mux0, null);
        var CTRL_MT32_V1_04_B = new ROMInfo(32768, CTRL_MT32_V1_04_B_SHA1, Type.Control, "ctrl_mt32_1_04_b", "MT-32 Control v1.04", PairType.Mux1, CTRL_MT32_V1_04_A);
        var CTRL_MT32_V1_04 = new ROMInfo(65536, CTRL_MT32_V1_04_SHA1, Type.Control, "ctrl_mt32_1_04", "MT-32 Control v1.04", PairType.Full, null);
        var CTRL_MT32_V1_05_A = new ROMInfo(32768, CTRL_MT32_V1_05_A_SHA1, Type.Control, "ctrl_mt32_1_05_a", "MT-32 Control v1.05", PairType.Mux0, null);
        var CTRL_MT32_V1_05_B = new ROMInfo(32768, CTRL_MT32_V1_05_B_SHA1, Type.Control, "ctrl_mt32_1_05_b", "MT-32 Control v1.05", PairType.Mux1, CTRL_MT32_V1_05_A);
        var CTRL_MT32_V1_05 = new ROMInfo(65536, CTRL_MT32_V1_05_SHA1, Type.Control, "ctrl_mt32_1_05", "MT-32 Control v1.05", PairType.Full, null);
        var CTRL_MT32_V1_06_A = new ROMInfo(32768, CTRL_MT32_V1_06_A_SHA1, Type.Control, "ctrl_mt32_1_06_a", "MT-32 Control v1.06", PairType.Mux0, null);
        var CTRL_MT32_V1_06_B = new ROMInfo(32768, CTRL_MT32_V1_06_B_SHA1, Type.Control, "ctrl_mt32_1_06_b", "MT-32 Control v1.06", PairType.Mux1, CTRL_MT32_V1_06_A);
        var CTRL_MT32_V1_06 = new ROMInfo(65536, CTRL_MT32_V1_06_SHA1, Type.Control, "ctrl_mt32_1_06", "MT-32 Control v1.06", PairType.Full, null);
        var CTRL_MT32_V1_07_A = new ROMInfo(32768, CTRL_MT32_V1_07_A_SHA1, Type.Control, "ctrl_mt32_1_07_a", "MT-32 Control v1.07", PairType.Mux0, null);
        var CTRL_MT32_V1_07_B = new ROMInfo(32768, CTRL_MT32_V1_07_B_SHA1, Type.Control, "ctrl_mt32_1_07_b", "MT-32 Control v1.07", PairType.Mux1, CTRL_MT32_V1_07_A);
        var CTRL_MT32_V1_07 = new ROMInfo(65536, CTRL_MT32_V1_07_SHA1, Type.Control, "ctrl_mt32_1_07", "MT-32 Control v1.07", PairType.Full, null);
        var CTRL_MT32_BLUER_A = new ROMInfo(32768, CTRL_MT32_BLUER_A_SHA1, Type.Control, "ctrl_mt32_bluer_a", "MT-32 Control BlueRidge", PairType.Mux0, null);
        var CTRL_MT32_BLUER_B = new ROMInfo(32768, CTRL_MT32_BLUER_B_SHA1, Type.Control, "ctrl_mt32_bluer_b", "MT-32 Control BlueRidge", PairType.Mux1, CTRL_MT32_BLUER_A);
        var CTRL_MT32_BLUER = new ROMInfo(65536, CTRL_MT32_BLUER_SHA1, Type.Control, "ctrl_mt32_bluer", "MT-32 Control BlueRidge", PairType.Full, null);

        var CTRL_MT32_V2_03 = new ROMInfo(131072, CTRL_MT32_V2_03_SHA1, Type.Control, "ctrl_mt32_2_03", "MT-32 Control v2.03", PairType.Full, null);
        var CTRL_MT32_V2_04 = new ROMInfo(131072, CTRL_MT32_V2_04_SHA1, Type.Control, "ctrl_mt32_2_04", "MT-32 Control v2.04", PairType.Full, null);
        var CTRL_MT32_V2_06 = new ROMInfo(131072, CTRL_MT32_V2_06_SHA1, Type.Control, "ctrl_mt32_2_06", "MT-32 Control v2.06", PairType.Full, null);
        var CTRL_MT32_V2_07 = new ROMInfo(131072, CTRL_MT32_V2_07_SHA1, Type.Control, "ctrl_mt32_2_07", "MT-32 Control v2.07", PairType.Full, null);
        var CTRL_CM32L_V1_00 = new ROMInfo(65536, CTRL_CM32L_V1_00_SHA1, Type.Control, "ctrl_cm32l_1_00", "CM-32L/LAPC-I Control v1.00", PairType.Full, null);
        var CTRL_CM32L_V1_02 = new ROMInfo(65536, CTRL_CM32L_V1_02_SHA1, Type.Control, "ctrl_cm32l_1_02", "CM-32L/LAPC-I Control v1.02", PairType.Full, null);
        var CTRL_CM32LN_V1_00 = new ROMInfo(65536, CTRL_CM32LN_V1_00_SHA1, Type.Control, "ctrl_cm32ln_1_00", "CM-32LN/CM-500/LAPC-N Control v1.00", PairType.Full, null);

        // ROM Info objects - PCM ROMs
        var PCM_MT32_L = new ROMInfo(262144, PCM_MT32_L_SHA1, Type.PCM, "pcm_mt32_l", "MT-32 PCM ROM", PairType.FirstHalf, null);
        var PCM_MT32_H = new ROMInfo(262144, PCM_MT32_H_SHA1, Type.PCM, "pcm_mt32_h", "MT-32 PCM ROM", PairType.SecondHalf, PCM_MT32_L);
        var PCM_MT32 = new ROMInfo(524288, PCM_MT32_SHA1, Type.PCM, "pcm_mt32", "MT-32 PCM ROM", PairType.Full, null);
        // Alias of PCM_MT32 ROM, only useful for pairing with PCM_CM32L_H.
        var PCM_CM32L_L = new ROMInfo(524288, PCM_MT32_SHA1, Type.PCM, "pcm_cm32l_l", "CM-32L/CM-64/LAPC-I PCM ROM", PairType.FirstHalf, null);
        var PCM_CM32L_H = new ROMInfo(524288, PCM_CM32L_H_SHA1, Type.PCM, "pcm_cm32l_h", "CM-32L/CM-64/LAPC-I PCM ROM", PairType.SecondHalf, PCM_CM32L_L);
        var PCM_CM32L = new ROMInfo(1048576, PCM_CM32L_SHA1, Type.PCM, "pcm_cm32l", "CM-32L/CM-64/LAPC-I PCM ROM", PairType.Full, null);

        // Set up pair relationships
        CTRL_MT32_V1_04_A.pairROMInfo = CTRL_MT32_V1_04_B;
        CTRL_MT32_V1_05_A.pairROMInfo = CTRL_MT32_V1_05_B;
        CTRL_MT32_V1_06_A.pairROMInfo = CTRL_MT32_V1_06_B;
        CTRL_MT32_V1_07_A.pairROMInfo = CTRL_MT32_V1_07_B;
        CTRL_MT32_BLUER_A.pairROMInfo = CTRL_MT32_BLUER_B;
        PCM_MT32_L.pairROMInfo = PCM_MT32_H;
        PCM_CM32L_L.pairROMInfo = PCM_CM32L_H;

        // Full ROM list
        ROMInfo?[] FULL_ROM_INFOS = new ROMInfo?[]
        {
            CTRL_MT32_V1_04,
            CTRL_MT32_V1_05,
            CTRL_MT32_V1_06,
            CTRL_MT32_V1_07,
            CTRL_MT32_BLUER,
            CTRL_MT32_V2_03,
            CTRL_MT32_V2_04,
            CTRL_MT32_V2_06,
            CTRL_MT32_V2_07,
            CTRL_CM32L_V1_00,
            CTRL_CM32L_V1_02,
            CTRL_CM32LN_V1_00,
            PCM_MT32,
            PCM_CM32L,
            null
        };

        // Partial ROM list
        ROMInfo?[] PARTIAL_ROM_INFOS = new ROMInfo?[]
        {
            CTRL_MT32_V1_04_A, CTRL_MT32_V1_04_B,
            CTRL_MT32_V1_05_A, CTRL_MT32_V1_05_B,
            CTRL_MT32_V1_06_A, CTRL_MT32_V1_06_B,
            CTRL_MT32_V1_07_A, CTRL_MT32_V1_07_B,
            CTRL_MT32_BLUER_A, CTRL_MT32_BLUER_B,
            PCM_MT32_L, PCM_MT32_H,
            PCM_CM32L_L, PCM_CM32L_H,
            null
        };

        // All ROM list
        Bit32u fullCount = CalcArrayLength(FULL_ROM_INFOS);
        Bit32u partialCount = CalcArrayLength(PARTIAL_ROM_INFOS);
        ROMInfo?[] ALL_ROM_INFOS = new ROMInfo?[fullCount + partialCount + 1];
        Array.Copy(FULL_ROM_INFOS, 0, ALL_ROM_INFOS, 0, fullCount);
        Array.Copy(PARTIAL_ROM_INFOS, 0, ALL_ROM_INFOS, fullCount, partialCount + 1); // Includes null terminator

        // Machine-specific ROM lists
        ROMInfo?[] MT32_V1_04_ROMS = new ROMInfo?[] { CTRL_MT32_V1_04, PCM_MT32, CTRL_MT32_V1_04_A, CTRL_MT32_V1_04_B, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V1_05_ROMS = new ROMInfo?[] { CTRL_MT32_V1_05, PCM_MT32, CTRL_MT32_V1_05_A, CTRL_MT32_V1_05_B, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V1_06_ROMS = new ROMInfo?[] { CTRL_MT32_V1_06, PCM_MT32, CTRL_MT32_V1_06_A, CTRL_MT32_V1_06_B, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V1_07_ROMS = new ROMInfo?[] { CTRL_MT32_V1_07, PCM_MT32, CTRL_MT32_V1_07_A, CTRL_MT32_V1_07_B, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_BLUER_ROMS = new ROMInfo?[] { CTRL_MT32_BLUER, PCM_MT32, CTRL_MT32_BLUER_A, CTRL_MT32_BLUER_B, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V2_03_ROMS = new ROMInfo?[] { CTRL_MT32_V2_03, PCM_MT32, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V2_04_ROMS = new ROMInfo?[] { CTRL_MT32_V2_04, PCM_MT32, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V2_06_ROMS = new ROMInfo?[] { CTRL_MT32_V2_06, PCM_MT32, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] MT32_V2_07_ROMS = new ROMInfo?[] { CTRL_MT32_V2_07, PCM_MT32, PCM_MT32_L, PCM_MT32_H, null };
        ROMInfo?[] CM32L_V1_00_ROMS = new ROMInfo?[] { CTRL_CM32L_V1_00, PCM_CM32L, PCM_CM32L_L, PCM_CM32L_H, null };
        ROMInfo?[] CM32L_V1_02_ROMS = new ROMInfo?[] { CTRL_CM32L_V1_02, PCM_CM32L, PCM_CM32L_L, PCM_CM32L_H, null };
        ROMInfo?[] CM32LN_V1_00_ROMS = new ROMInfo?[] { CTRL_CM32LN_V1_00, PCM_CM32L, null };

        s_romInfoLists = new ROMInfoLists(
            new ROMInfoList(MT32_V1_04_ROMS, CalcArrayLength(MT32_V1_04_ROMS)),
            new ROMInfoList(MT32_V1_05_ROMS, CalcArrayLength(MT32_V1_05_ROMS)),
            new ROMInfoList(MT32_V1_06_ROMS, CalcArrayLength(MT32_V1_06_ROMS)),
            new ROMInfoList(MT32_V1_07_ROMS, CalcArrayLength(MT32_V1_07_ROMS)),
            new ROMInfoList(MT32_BLUER_ROMS, CalcArrayLength(MT32_BLUER_ROMS)),
            new ROMInfoList(MT32_V2_03_ROMS, CalcArrayLength(MT32_V2_03_ROMS)),
            new ROMInfoList(MT32_V2_04_ROMS, CalcArrayLength(MT32_V2_04_ROMS)),
            new ROMInfoList(MT32_V2_06_ROMS, CalcArrayLength(MT32_V2_06_ROMS)),
            new ROMInfoList(MT32_V2_07_ROMS, CalcArrayLength(MT32_V2_07_ROMS)),
            new ROMInfoList(CM32L_V1_00_ROMS, CalcArrayLength(CM32L_V1_00_ROMS)),
            new ROMInfoList(CM32L_V1_02_ROMS, CalcArrayLength(CM32L_V1_02_ROMS)),
            new ROMInfoList(CM32LN_V1_00_ROMS, CalcArrayLength(CM32LN_V1_00_ROMS)),
            new ROMInfoList(FULL_ROM_INFOS, CalcArrayLength(FULL_ROM_INFOS)),
            new ROMInfoList(PARTIAL_ROM_INFOS, CalcArrayLength(PARTIAL_ROM_INFOS)),
            new ROMInfoList(ALL_ROM_INFOS, CalcArrayLength(ALL_ROM_INFOS))
        );

        return s_romInfoLists;
    }

    internal static ROMInfo?[] GetKnownROMInfoList()
    {
        return GetROMInfoLists().allROMInfos.romInfos;
    }

    private static ROMInfo? GetKnownROMInfoFromList(Bit32u index)
    {
        var list = GetKnownROMInfoList();
        return index < list.Length ? list[index] : null;
    }
}

// Synth.Open() requires a full control ROMImage and a compatible full PCM ROMImage to work

public class ROMImage : IDisposable
{
    private readonly IFile file;
    private readonly bool ownFile;
    private readonly ROMInfo? romInfo;

    private ROMImage(IFile useFile, bool useOwnFile, ROMInfo?[] romInfos)
    {
        file = useFile;
        ownFile = useOwnFile;
        romInfo = ROMInfo.GetROMInfo(file, romInfos);
    }

    ~ROMImage()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        ROMInfo.FreeROMInfo(romInfo);
        if (ownFile && disposing)
        {
            file?.Close();
        }
    }

    // Creates a ROMImage object given a ROMInfo and a IFile. Keeps a reference
    // to the IFile and ROMInfo given, which must be freed separately by the user
    // after the ROMImage is freed.
    public static ROMImage? MakeROMImage(IFile file)
    {
        return new ROMImage(file, false, ROMInfo.GetKnownROMInfoList());
    }

    // Same as the method above but only permits creation of a ROMImage if the file content
    // matches one of the ROMs described in a list romInfos.
    public static ROMImage? MakeROMImage(IFile file, ROMInfo?[] romInfos)
    {
        return new ROMImage(file, false, romInfos);
    }

    // Creates a ROMImage object given a couple of files that contain compatible partial ROM images.
    public static ROMImage? MakeROMImage(IFile file1, IFile file2)
    {
        ROMInfo?[] partialROMInfos = ROMInfo.GetPartialROMInfos();
        ROMImage? image1 = MakeROMImage(file1, partialROMInfos);
        ROMImage? image2 = MakeROMImage(file2, partialROMInfos);
        
        if (image1 == null || image2 == null)
        {
            image1?.Dispose();
            image2?.Dispose();
            return null;
        }

        ROMImage? fullImage = (image1.GetROMInfo() == null || image2.GetROMInfo() == null) ? null : MergeROMImages(image1, image2);
        image1.Dispose();
        image2.Dispose();
        return fullImage;
    }

    // Must only be done after all Synths using the ROMImage are deleted
    public static void FreeROMImage(ROMImage? romImage)
    {
        romImage?.Dispose();
    }

    // Checks whether the given ROMImages are pairable and merges them into a full image, if possible.
    public static ROMImage? MergeROMImages(ROMImage romImage1, ROMImage romImage2)
    {
        if (romImage1.romInfo?.pairROMInfo != romImage2.romInfo)
        {
            return null;
        }
        
        switch (romImage1.romInfo?.pairType)
        {
            case ROMInfo.PairType.FirstHalf:
                return AppendImages(romImage1, romImage2);
            case ROMInfo.PairType.SecondHalf:
                return AppendImages(romImage2, romImage1);
            case ROMInfo.PairType.Mux0:
                return InterleaveImages(romImage1, romImage2);
            case ROMInfo.PairType.Mux1:
                return InterleaveImages(romImage2, romImage1);
            default:
                break;
        }
        return null;
    }

    public IFile? GetFile()
    {
        return file;
    }

    // Returns true in case this ROMImage is built with a user provided IFile that has to be deallocated separately.
    public bool IsFileUserProvided()
    {
        return !ownFile;
    }

    public ROMInfo? GetROMInfo()
    {
        return romInfo;
    }

    private static ROMImage? MakeFullROMImage(Bit8u[] data, nuint dataSize)
    {
        return new ROMImage(new ArrayFile(data.AsSpan()), true, ROMInfo.GetKnownROMInfoList());
    }

    private static ROMImage? AppendImages(ROMImage romImageLow, ROMImage romImageHigh)
    {
        IFile? fileLow = romImageLow.GetFile();
        IFile? fileHigh = romImageHigh.GetFile();
        if (fileLow == null || fileHigh == null) return null;
        
        ReadOnlySpan<Bit8u> romDataLow = fileLow.GetData();
        ReadOnlySpan<Bit8u> romDataHigh = fileHigh.GetData();
        nuint partSize = fileLow.GetSize();
        
        Bit8u[] data = new Bit8u[2 * partSize];
        romDataLow.CopyTo(data.AsSpan(0, (int)partSize));
        romDataHigh.CopyTo(data.AsSpan((int)partSize, (int)partSize));
        
        ROMImage? romImageFull = MakeFullROMImage(data, 2 * partSize);
        if (romImageFull?.GetROMInfo() == null)
        {
            FreeROMImage(romImageFull);
            return null;
        }
        return romImageFull;
    }

    private static ROMImage? InterleaveImages(ROMImage romImageEven, ROMImage romImageOdd)
    {
        IFile? fileEven = romImageEven.GetFile();
        IFile? fileOdd = romImageOdd.GetFile();
        if (fileEven == null || fileOdd == null) return null;
        
        ReadOnlySpan<Bit8u> romDataEven = fileEven.GetData();
        ReadOnlySpan<Bit8u> romDataOdd = fileOdd.GetData();
        nuint partSize = fileEven.GetSize();
        
        Bit8u[] data = new Bit8u[2 * partSize];
        int writePtr = 0;
        for (nuint romDataIx = 0; romDataIx < partSize; romDataIx++)
        {
            data[writePtr++] = romDataEven[(int)romDataIx];
            data[writePtr++] = romDataOdd[(int)romDataIx];
        }
        
        ROMImage? romImageFull = MakeFullROMImage(data, 2 * partSize);
        if (romImageFull?.GetROMInfo() == null)
        {
            FreeROMImage(romImageFull);
            return null;
        }
        return romImageFull;
    }
}

public class MachineConfiguration
{
    private readonly string machineID;
    private readonly ROMInfo?[] romInfos;
    private readonly Bit32u romInfosCount;

    private MachineConfiguration(string machineID, ROMInfo?[] romInfos, Bit32u romInfosCount)
    {
        this.machineID = machineID;
        this.romInfos = romInfos;
        this.romInfosCount = romInfosCount;
    }

    // Returns an immutable list of all supported machine configurations.
    public static MachineConfiguration[] GetAllMachineConfigurations(out Bit32u? itemCount)
    {
        var romInfoLists = ROMInfo.GetROMInfoLists();
        
        MachineConfiguration[] MACHINE_CONFIGURATIONS = new MachineConfiguration[]
        {
            new MachineConfiguration("mt32_1_04", romInfoLists.mt32_1_04.romInfos, romInfoLists.mt32_1_04.itemCount),
            new MachineConfiguration("mt32_1_05", romInfoLists.mt32_1_05.romInfos, romInfoLists.mt32_1_05.itemCount),
            new MachineConfiguration("mt32_1_06", romInfoLists.mt32_1_06.romInfos, romInfoLists.mt32_1_06.itemCount),
            new MachineConfiguration("mt32_1_07", romInfoLists.mt32_1_07.romInfos, romInfoLists.mt32_1_07.itemCount),
            new MachineConfiguration("mt32_bluer", romInfoLists.mt32_bluer.romInfos, romInfoLists.mt32_bluer.itemCount),
            new MachineConfiguration("mt32_2_03", romInfoLists.mt32_2_03.romInfos, romInfoLists.mt32_2_03.itemCount),
            new MachineConfiguration("mt32_2_04", romInfoLists.mt32_2_04.romInfos, romInfoLists.mt32_2_04.itemCount),
            new MachineConfiguration("mt32_2_06", romInfoLists.mt32_2_06.romInfos, romInfoLists.mt32_2_06.itemCount),
            new MachineConfiguration("mt32_2_07", romInfoLists.mt32_2_07.romInfos, romInfoLists.mt32_2_07.itemCount),
            new MachineConfiguration("cm32l_1_00", romInfoLists.cm32l_1_00.romInfos, romInfoLists.cm32l_1_00.itemCount),
            new MachineConfiguration("cm32l_1_02", romInfoLists.cm32l_1_02.romInfos, romInfoLists.cm32l_1_02.itemCount),
            new MachineConfiguration("cm32ln_1_00", romInfoLists.cm32ln_1_00.romInfos, romInfoLists.cm32ln_1_00.itemCount)
        };

        itemCount = (Bit32u)MACHINE_CONFIGURATIONS.Length;
        return MACHINE_CONFIGURATIONS;
    }

    public static MachineConfiguration[] GetAllMachineConfigurations()
    {
        return GetAllMachineConfigurations(out _);
    }

    // Returns a string identifier of this MachineConfiguration.
    public string GetMachineID()
    {
        return machineID;
    }

    // Returns an immutable list of ROMInfos that are compatible with this MachineConfiguration.
    public ROMInfo?[] GetCompatibleROMInfos(out Bit32u? itemCount)
    {
        itemCount = romInfosCount;
        return romInfos;
    }

    public ROMInfo?[] GetCompatibleROMInfos()
    {
        return romInfos;
    }
}

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

namespace MT32Emu;

using Bit8u = System.Byte;
using Bit16u = System.UInt16;

public sealed class Tables
{
    private static readonly Lazy<Tables> _instance = new Lazy<Tables>(() => new Tables());

    // Ensures a singleton that is immutable yet guaranteed to be initialised orderly.
    // However, this normally adds some performance penalty, so it should be avoided on the critical path.
    public static Tables GetInstance() => _instance.Value;

    // Constant LUTs

    // CONFIRMED: This is used to convert several parameters to amp-modifying values in the TVA envelope:
    // - PatchTemp.outputLevel
    // - RhythmTemp.outlevel
    // - PartialParam.tva.level
    // - expression
    // It's used to determine how much to subtract from the amp envelope's target value
    public readonly Bit8u[] levelToAmpSubtraction = new Bit8u[101];

    // CONFIRMED: ...
    public readonly Bit8u[] envLogarithmicTime = new Bit8u[256];

    // CONFIRMED: ...
    public readonly Bit8u[] masterVolToAmpSubtraction = new Bit8u[101];

    // CONFIRMED:
    public readonly Bit8u[] pulseWidth100To255 = new Bit8u[101];

    public readonly Bit16u[] exp9 = new Bit16u[512];
    public readonly Bit16u[] logsin9 = new Bit16u[512];

    public readonly Bit8u[] resAmpDecayFactors;

    private Tables()
    {
        for (int lf = 0; lf <= 100; lf++)
        {
            // CONFIRMED:KG: This matches a ROM table found by Mok
            float fVal = (2.0f - MMath.LOG10F(lf + 1.0f)) * 128.0f;
            int val = (int)(fVal + 1.0);
            if (val > 255)
            {
                val = 255;
            }
            levelToAmpSubtraction[lf] = (Bit8u)val;
        }

        envLogarithmicTime[0] = 64;
        for (int lf = 1; lf <= 255; lf++)
        {
            // CONFIRMED:KG: This matches a ROM table found by Mok
            envLogarithmicTime[lf] = (Bit8u)Math.Ceiling(64.0f + MMath.LOG2F(lf) * 8.0f);
        }

        // CONFIRMED: Based on a table found by Mok in the MT-32 control ROM
        masterVolToAmpSubtraction[0] = 255;
        for (int masterVol = 1; masterVol <= 100; masterVol++)
        {
            masterVolToAmpSubtraction[masterVol] = (Bit8u)(106.31 - 16.0f * MMath.LOG2F(masterVol));
        }

        for (int i = 0; i <= 100; i++)
        {
            pulseWidth100To255[i] = (Bit8u)(i * 255 / 100.0f + 0.5f);
        }

        // The LA32 chip contains an exponent table inside. The table contains 12-bit integer values.
        // The actual table size is 512 rows. The 9 higher bits of the fractional part of the argument are used as a lookup address.
        // To improve the precision of computations, the lower bits are supposed to be used for interpolation as the LA32 chip also
        // contains another 512-row table with inverted differences between the main table values.
        for (int i = 0; i < 512; i++)
        {
            exp9[i] = (Bit16u)(8191.5f - MMath.EXP2F(13.0f + ~i / 512.0f));
        }

        // There is a logarithmic sine table inside the LA32 chip. The table contains 13-bit integer values.
        for (int i = 1; i < 512; i++)
        {
            logsin9[i] = (Bit16u)(0.5f - MMath.LOG2F((float)Math.Sin((i + 0.5f) / 1024.0f * MMath.FLOAT_PI)) * 1024.0f);
        }

        // The very first value is clamped to the maximum possible 13-bit integer
        logsin9[0] = 8191;

        // found from sample analysis
        resAmpDecayFactors = new Bit8u[] { 31, 16, 12, 8, 5, 3, 2, 1 };
    }
}

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

public static class MMath
{
    // Mathematical constants
    public const double DOUBLE_PI = 3.141592653589793;
    public const double DOUBLE_LN_10 = 2.302585092994046;
    public const float FLOAT_PI = 3.1415927f;
    public const float FLOAT_2PI = 6.2831853f;
    public const float FLOAT_LN_2 = 0.6931472f;
    public const float FLOAT_LN_10 = 2.3025851f;

    public static float POWF(float x, float y)
    {
        return (float)Math.Pow(x, y);
    }

    public static float EXPF(float x)
    {
        return (float)Math.Exp(x);
    }

    public static float EXP2F(float x)
    {
        return (float)Math.Exp(FLOAT_LN_2 * x);
    }

    public static float EXP10F(float x)
    {
        return (float)Math.Exp(FLOAT_LN_10 * x);
    }

    public static float LOGF(float x)
    {
        return (float)Math.Log(x);
    }

    public static float LOG2F(float x)
    {
        return (float)(Math.Log(x) / FLOAT_LN_2);
    }

    public static float LOG10F(float x)
    {
        return (float)Math.Log10(x);
    }
}

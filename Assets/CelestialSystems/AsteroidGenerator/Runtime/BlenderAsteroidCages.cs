// Base cage coordinate recipes translated from the supplied rockgen.py.
using System.Collections.Generic;
using UnityEngine;
namespace jcan.CelestialSystems
{
    public static partial class BlenderAsteroidGenerator
    {
        private static Surface CreateCage(BlenderAsteroidSettings s, RandomStream rng, out Vector3 restoreScale)
        {
            int shape = s.BaseShape < 0 ? rng.Integer(12) : Mathf.Clamp(s.BaseShape, 0, 11);
            var x = new List<float>(); var y = new List<float>(); var z = new List<float>();
            var scaleX = Ordered(s.ScaleX); var scaleY = Ordered(s.ScaleY); var scaleZ = Ordered(s.ScaleZ);
            float muX, muY, muZ, sigmaX, sigmaY, sigmaZ;
            bool upperSkewX, upperSkewY, upperSkewZ;
            Distribution(scaleX, s.Skew.x, out muX, out sigmaX, out upperSkewX);
            Distribution(scaleY, s.Skew.y, out muY, out sigmaY, out upperSkewY);
            Distribution(scaleZ, s.Skew.z, out muZ, out sigmaZ, out upperSkewZ);
            if (shape == 0)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (sigmaX == 0)
                    {
                        x.Add((scaleX[0] / 2));
                    }
                    else
                    {
                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                    }
                    if (sigmaY == 0)
                    {
                        y.Add((scaleY[0] / 2));
                    }
                    else
                    {
                        y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                    }
                    if (sigmaZ == 0)
                    {
                        z.Add((scaleZ[0] / 2));
                    }
                    else
                    {
                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                    }
                }
            }
            else
            {
                if (shape == 1)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if ((j == 0 || j == 1 || j == 3 || j == 4))
                        {
                            if (sigmaX == 0)
                            {
                                x.Add((scaleX[0] / 2));
                            }
                            else
                            {
                                x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                            }
                            if (sigmaY == 0)
                            {
                                y.Add((scaleY[0] / 2));
                            }
                            else
                            {
                                y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                            }
                            if (sigmaZ == 0)
                            {
                                z.Add((scaleZ[0] / 2));
                            }
                            else
                            {
                                z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                            }
                        }
                        else
                        {
                            if ((j == 2 || j == 5))
                            {
                                if (sigmaX == 0)
                                {
                                    x.Add(0);
                                }
                                else
                                {
                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 4));
                                }
                                if (sigmaY == 0)
                                {
                                    y.Add((scaleY[0] / 2));
                                }
                                else
                                {
                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                }
                                if (sigmaZ == 0)
                                {
                                    z.Add((scaleZ[0] / 2));
                                }
                                else
                                {
                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                }
                            }
                            else
                            {
                                if ((j == 6 || j == 7))
                                {
                                    if (sigmaX == 0)
                                    {
                                        x.Add(0);
                                    }
                                    else
                                    {
                                        x.Add((SkewedGaussian(0, sigmaX, scaleX, upperSkewX, rng) / 4));
                                    }
                                    if (sigmaY == 0)
                                    {
                                        y.Add(0);
                                    }
                                    else
                                    {
                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 4));
                                    }
                                    if (sigmaZ == 0)
                                    {
                                        z.Add((scaleZ[0] / 2));
                                    }
                                    else
                                    {
                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (shape == 2)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            if ((j == 0 || j == 2 || j == 5 || j == 7))
                            {
                                if (sigmaX == 0)
                                {
                                    x.Add((scaleX[0] / 4));
                                }
                                else
                                {
                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 4));
                                }
                                if (sigmaY == 0)
                                {
                                    y.Add(0);
                                }
                                else
                                {
                                    y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 4));
                                }
                                if (sigmaZ == 0)
                                {
                                    z.Add((scaleZ[0] / 2));
                                }
                                else
                                {
                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 4));
                                }
                            }
                            else
                            {
                                if ((j == 1 || j == 3 || j == 4 || j == 6))
                                {
                                    if (sigmaX == 0)
                                    {
                                        x.Add((scaleX[0] / 2));
                                    }
                                    else
                                    {
                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                    }
                                    if (sigmaY == 0)
                                    {
                                        y.Add((scaleY[0] / 2));
                                    }
                                    else
                                    {
                                        y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                    }
                                    if (sigmaZ == 0)
                                    {
                                        z.Add((scaleZ[0] / 2));
                                    }
                                    else
                                    {
                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (shape == 3)
                        {
                            for (int j = 0; j < 8; j++)
                            {
                                if (j > 0)
                                {
                                    if (sigmaX == 0)
                                    {
                                        x.Add((scaleX[0] / 2));
                                    }
                                    else
                                    {
                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                    }
                                    if (sigmaY == 0)
                                    {
                                        y.Add((scaleY[0] / 2));
                                    }
                                    else
                                    {
                                        y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                    }
                                    if (sigmaZ == 0)
                                    {
                                        z.Add((scaleZ[0] / 2));
                                    }
                                    else
                                    {
                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                    }
                                }
                                else
                                {
                                    if (sigmaX == 0)
                                    {
                                        x.Add(0);
                                    }
                                    else
                                    {
                                        x.Add((SkewedGaussian(0, sigmaX, scaleX, upperSkewX, rng) / 8));
                                    }
                                    if (sigmaY == 0)
                                    {
                                        y.Add(0);
                                    }
                                    else
                                    {
                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 8));
                                    }
                                    if (sigmaZ == 0)
                                    {
                                        z.Add(0);
                                    }
                                    else
                                    {
                                        z.Add((SkewedGaussian(0, sigmaZ, scaleZ, upperSkewZ, rng) / 8));
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (shape == 4)
                            {
                                for (int j = 0; j < 10; j++)
                                {
                                    if ((j == 0 || j == 9))
                                    {
                                        if (sigmaX == 0)
                                        {
                                            x.Add(0);
                                        }
                                        else
                                        {
                                            x.Add((SkewedGaussian(0, sigmaX, scaleX, upperSkewX, rng) / 2));
                                        }
                                        if (sigmaY == 0)
                                        {
                                            y.Add(0);
                                        }
                                        else
                                        {
                                            y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 2));
                                        }
                                        if (sigmaZ == 0)
                                        {
                                            z.Add((scaleZ[0] / 2));
                                        }
                                        else
                                        {
                                            z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                        }
                                    }
                                    else
                                    {
                                        if ((j == 1 || j == 2 || j == 3 || j == 4))
                                        {
                                            if (sigmaX == 0)
                                            {
                                                x.Add((scaleX[0] / 2));
                                            }
                                            else
                                            {
                                                x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                            }
                                            if (sigmaY == 0)
                                            {
                                                y.Add((scaleY[0] / 2));
                                            }
                                            else
                                            {
                                                y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                            }
                                            if (sigmaZ == 0)
                                            {
                                                z.Add((scaleZ[0] / 2));
                                            }
                                            else
                                            {
                                                z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                            }
                                        }
                                        else
                                        {
                                            if ((j == 5 || j == 7))
                                            {
                                                if (sigmaX == 0)
                                                {
                                                    x.Add(0);
                                                }
                                                else
                                                {
                                                    x.Add((SkewedGaussian(0, sigmaX, scaleX, upperSkewX, rng) / 3));
                                                }
                                                if (sigmaY == 0)
                                                {
                                                    y.Add((scaleY[0] / 3));
                                                }
                                                else
                                                {
                                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 3));
                                                }
                                                if (sigmaZ == 0)
                                                {
                                                    z.Add(0);
                                                }
                                                else
                                                {
                                                    z.Add((SkewedGaussian(0, sigmaZ, scaleZ, upperSkewZ, rng) / 6));
                                                }
                                            }
                                            else
                                            {
                                                if ((j == 6 || j == 8))
                                                {
                                                    if (sigmaX == 0)
                                                    {
                                                        x.Add((scaleX[0] / 3));
                                                    }
                                                    else
                                                    {
                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 3));
                                                    }
                                                    if (sigmaY == 0)
                                                    {
                                                        y.Add(0);
                                                    }
                                                    else
                                                    {
                                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 3));
                                                    }
                                                    if (sigmaZ == 0)
                                                    {
                                                        z.Add(0);
                                                    }
                                                    else
                                                    {
                                                        z.Add((SkewedGaussian(0, sigmaZ, scaleZ, upperSkewZ, rng) / 6));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (shape == 5)
                                {
                                    for (int j = 0; j < 10; j++)
                                    {
                                        if (j == 0)
                                        {
                                            if (sigmaX == 0)
                                            {
                                                x.Add(0);
                                            }
                                            else
                                            {
                                                x.Add((SkewedGaussian(0, sigmaX, scaleX, upperSkewX, rng) / 8));
                                            }
                                            if (sigmaY == 0)
                                            {
                                                y.Add(0);
                                            }
                                            else
                                            {
                                                y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 8));
                                            }
                                            if (sigmaZ == 0)
                                            {
                                                z.Add((scaleZ[0] / 2));
                                            }
                                            else
                                            {
                                                z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                            }
                                        }
                                        else
                                        {
                                            if ((j == 1 || j == 2))
                                            {
                                                if (sigmaX == 0)
                                                {
                                                    x.Add((scaleZ[0] * 0.125f));
                                                }
                                                else
                                                {
                                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) * 0.125f));
                                                }
                                                if (sigmaY == 0)
                                                {
                                                    y.Add((scaleZ[0] * 0.2165f));
                                                }
                                                else
                                                {
                                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) * 0.2165f));
                                                }
                                                if (sigmaZ == 0)
                                                {
                                                    z.Add(0);
                                                }
                                                else
                                                {
                                                    z.Add((SkewedGaussian(0, sigmaZ, scaleZ, upperSkewZ, rng) / 4));
                                                }
                                            }
                                            else
                                            {
                                                if (j == 3)
                                                {
                                                    if (sigmaX == 0)
                                                    {
                                                        x.Add((scaleX[0] / 4));
                                                    }
                                                    else
                                                    {
                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 4));
                                                    }
                                                    if (sigmaY == 0)
                                                    {
                                                        y.Add(0);
                                                    }
                                                    else
                                                    {
                                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 4));
                                                    }
                                                    if (sigmaZ == 0)
                                                    {
                                                        z.Add(0);
                                                    }
                                                    else
                                                    {
                                                        z.Add((SkewedGaussian(0, sigmaZ, scaleZ, upperSkewZ, rng) / 4));
                                                    }
                                                }
                                                else
                                                {
                                                    if ((j == 4 || j == 6))
                                                    {
                                                        if (sigmaX == 0)
                                                        {
                                                            x.Add((scaleX[0] * 0.25f));
                                                        }
                                                        else
                                                        {
                                                            x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) * 0.25f));
                                                        }
                                                        if (sigmaY == 0)
                                                        {
                                                            y.Add((scaleY[0] * 0.433f));
                                                        }
                                                        else
                                                        {
                                                            y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) * 0.433f));
                                                        }
                                                        if (sigmaZ == 0)
                                                        {
                                                            z.Add((scaleZ[0] / 2));
                                                        }
                                                        else
                                                        {
                                                            z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (j == 5)
                                                        {
                                                            if (sigmaX == 0)
                                                            {
                                                                x.Add((scaleX[0] / 4));
                                                            }
                                                            else
                                                            {
                                                                x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 4));
                                                            }
                                                            if (sigmaY == 0)
                                                            {
                                                                y.Add(0);
                                                            }
                                                            else
                                                            {
                                                                y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                            }
                                                            if (sigmaZ == 0)
                                                            {
                                                                z.Add((scaleZ[0] / 2));
                                                            }
                                                            else
                                                            {
                                                                z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if ((j == 7 || j == 9))
                                                            {
                                                                if (sigmaX == 0)
                                                                {
                                                                    x.Add((scaleX[0] * 0.10825f));
                                                                }
                                                                else
                                                                {
                                                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) * 0.10825f));
                                                                }
                                                                if (sigmaY == 0)
                                                                {
                                                                    y.Add((scaleY[0] * 0.2165f));
                                                                }
                                                                else
                                                                {
                                                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) * 0.2165f));
                                                                }
                                                                if (sigmaZ == 0)
                                                                {
                                                                    z.Add((scaleZ[0] / 2));
                                                                }
                                                                else
                                                                {
                                                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (j == 8)
                                                                {
                                                                    if (sigmaX == 0)
                                                                    {
                                                                        x.Add((scaleX[0] / 2));
                                                                    }
                                                                    else
                                                                    {
                                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                                    }
                                                                    if (sigmaY == 0)
                                                                    {
                                                                        y.Add(0);
                                                                    }
                                                                    else
                                                                    {
                                                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 4));
                                                                    }
                                                                    if (sigmaZ == 0)
                                                                    {
                                                                        z.Add((scaleZ[0] / 2));
                                                                    }
                                                                    else
                                                                    {
                                                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (shape == 6)
                                    {
                                        for (int j = 0; j < 7; j++)
                                        {
                                            if (j > 0)
                                            {
                                                if (sigmaX == 0)
                                                {
                                                    x.Add((scaleX[0] / 2));
                                                }
                                                else
                                                {
                                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                }
                                                if (sigmaY == 0)
                                                {
                                                    y.Add((scaleY[0] / 2));
                                                }
                                                else
                                                {
                                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                }
                                                if (sigmaZ == 0)
                                                {
                                                    z.Add((scaleZ[0] / 2));
                                                }
                                                else
                                                {
                                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                }
                                            }
                                            else
                                            {
                                                if (sigmaX == 0)
                                                {
                                                    x.Add((scaleX[0] / 2));
                                                }
                                                else
                                                {
                                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                }
                                                if (sigmaY == 0)
                                                {
                                                    y.Add(0);
                                                }
                                                else
                                                {
                                                    y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                }
                                                if (sigmaZ == 0)
                                                {
                                                    z.Add((scaleZ[0] / 2));
                                                }
                                                else
                                                {
                                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (shape == 7)
                                        {
                                            for (int j = 0; j < 10; j++)
                                            {
                                                if ((j == 1 || j == 3 || j == 4 || j == 5 || j == 8 || j == 9))
                                                {
                                                    if (sigmaX == 0)
                                                    {
                                                        x.Add((scaleX[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                    }
                                                    if (sigmaY == 0)
                                                    {
                                                        y.Add((scaleY[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                    }
                                                    if (sigmaZ == 0)
                                                    {
                                                        z.Add((scaleZ[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                    }
                                                }
                                                else
                                                {
                                                    if (sigmaX == 0)
                                                    {
                                                        x.Add((scaleX[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                    }
                                                    if (sigmaY == 0)
                                                    {
                                                        y.Add(0);
                                                    }
                                                    else
                                                    {
                                                        y.Add((SkewedGaussian(0, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                    }
                                                    if (sigmaZ == 0)
                                                    {
                                                        z.Add((scaleZ[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (shape == 8)
                                            {
                                                for (int j = 0; j < 7; j++)
                                                {
                                                    if (sigmaX == 0)
                                                    {
                                                        x.Add((scaleX[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                    }
                                                    if (sigmaY == 0)
                                                    {
                                                        y.Add((scaleY[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                    }
                                                    if (sigmaZ == 0)
                                                    {
                                                        z.Add((scaleZ[0] / 2));
                                                    }
                                                    else
                                                    {
                                                        z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (shape == 9)
                                                {
                                                    for (int j = 0; j < 8; j++)
                                                    {
                                                        if (sigmaX == 0)
                                                        {
                                                            x.Add((scaleX[0] / 2));
                                                        }
                                                        else
                                                        {
                                                            x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                        }
                                                        if (sigmaY == 0)
                                                        {
                                                            y.Add((scaleY[0] / 2));
                                                        }
                                                        else
                                                        {
                                                            y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                        }
                                                        if (sigmaZ == 0)
                                                        {
                                                            z.Add((scaleZ[0] / 2));
                                                        }
                                                        else
                                                        {
                                                            z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (shape == 10)
                                                    {
                                                        for (int j = 0; j < 7; j++)
                                                        {
                                                            if (sigmaX == 0)
                                                            {
                                                                x.Add((scaleX[0] / 2));
                                                            }
                                                            else
                                                            {
                                                                x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                            }
                                                            if (sigmaY == 0)
                                                            {
                                                                y.Add((scaleY[0] / 2));
                                                            }
                                                            else
                                                            {
                                                                y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                            }
                                                            if (sigmaZ == 0)
                                                            {
                                                                z.Add((scaleZ[0] / 2));
                                                            }
                                                            else
                                                            {
                                                                z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (shape == 11)
                                                        {
                                                            for (int j = 0; j < 7; j++)
                                                            {
                                                                if (sigmaX == 0)
                                                                {
                                                                    x.Add((scaleX[0] / 2));
                                                                }
                                                                else
                                                                {
                                                                    x.Add((SkewedGaussian(muX, sigmaX, scaleX, upperSkewX, rng) / 2));
                                                                }
                                                                if (sigmaY == 0)
                                                                {
                                                                    y.Add((scaleY[0] / 2));
                                                                }
                                                                else
                                                                {
                                                                    y.Add((SkewedGaussian(muY, sigmaY, scaleY, upperSkewY, rng) / 2));
                                                                }
                                                                if (sigmaZ == 0)
                                                                {
                                                                    z.Add((scaleZ[0] / 2));
                                                                }
                                                                else
                                                                {
                                                                    z.Add((SkewedGaussian(muZ, sigmaZ, scaleZ, upperSkewZ, rng) / 2));
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            restoreScale = Vector3.one;
            if (s.ScaleTextures)
            {
                restoreScale = new Vector3(Average(x) * Mathf.Max(0.0001f, s.TextureScale.x), Average(y) * Mathf.Max(0.0001f, s.TextureScale.y), Average(z) * Mathf.Max(0.0001f, s.TextureScale.z));
                for (int i = 0; i < x.Count; i++) { x[i] /= restoreScale.x; y[i] /= restoreScale.y; z[i] /= restoreScale.z; }
            }
            List<Vector3> verts; List<int[]> faces;
            if (shape == 1)
            {
                verts = new List<Vector3> { new Vector3(-x[0], -y[0], -z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], -y[2], z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(x[4], y[4], -z[4]), new Vector3(x[5], y[5], z[5]), new Vector3(x[6], y[6], z[6]), new Vector3(x[7], y[7], -z[7]) };
                faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 1, 7 }, new int[] { 3, 0, 7 }, new int[] { 3, 4, 7 }, new int[] { 1, 4, 7 }, new int[] { 3, 4, 5 }, new int[] { 1, 2, 6 }, new int[] { 1, 4, 6 }, new int[] { 4, 5, 6 }, new int[] { 0, 2, 6 }, new int[] { 0, 3, 6 }, new int[] { 3, 5, 6 } };
            }
            else
            {
                if (shape == 2)
                {
                    verts = new List<Vector3> { new Vector3(-x[0], y[0], -z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(-x[4], -y[4], z[4]), new Vector3(x[5], y[5], z[5]), new Vector3(x[6], y[6], z[6]), new Vector3(-x[7], y[7], z[7]) };
                    faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 2, 3 }, new int[] { 0, 3, 7 }, new int[] { 0, 7, 4 }, new int[] { 1, 4, 5 }, new int[] { 0, 1, 4 }, new int[] { 5, 1, 2 }, new int[] { 5, 2, 6 }, new int[] { 3, 2, 6 }, new int[] { 3, 6, 7 }, new int[] { 5, 4, 7 }, new int[] { 5, 6, 7 } };
                }
                else
                {
                    if (shape == 3)
                    {
                        verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(x[4], -y[4], z[4]), new Vector3(x[5], y[5], z[5]), new Vector3(-x[6], y[6], z[6]), new Vector3(-x[7], -y[7], z[7]) };
                        faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 2, 3 }, new int[] { 0, 3, 6 }, new int[] { 0, 6, 7 }, new int[] { 0, 7, 4 }, new int[] { 0, 4, 1 }, new int[] { 5, 4, 1, 2 }, new int[] { 5, 6, 3, 2 }, new int[] { 5, 4, 7, 6 } };
                    }
                    else
                    {
                        if (shape == 4)
                        {
                            verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(-x[4], -y[4], -z[4]), new Vector3(x[5], -y[5], -z[5]), new Vector3(x[6], y[6], -z[6]), new Vector3(x[7], y[7], -z[7]), new Vector3(-x[8], y[8], -z[8]), new Vector3(x[9], y[9], -z[9]) };
                            faces = new List<int[]> { new int[] { 0, 1, 6 }, new int[] { 0, 6, 2 }, new int[] { 0, 2, 7 }, new int[] { 0, 7, 3 }, new int[] { 0, 3, 8 }, new int[] { 0, 8, 4 }, new int[] { 0, 4, 5 }, new int[] { 0, 5, 1 }, new int[] { 1, 9, 2 }, new int[] { 2, 9, 3 }, new int[] { 3, 9, 4 }, new int[] { 4, 9, 1 }, new int[] { 1, 6, 2 }, new int[] { 2, 7, 3 }, new int[] { 3, 8, 4 }, new int[] { 4, 5, 1 } };
                        }
                        else
                        {
                            if (shape == 5)
                            {
                                verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], z[1]), new Vector3(x[2], y[2], z[2]), new Vector3(-x[3], y[3], z[3]), new Vector3(x[4], -y[4], -z[4]), new Vector3(x[5], y[5], -z[5]), new Vector3(x[6], y[6], -z[6]), new Vector3(-x[7], y[7], -z[7]), new Vector3(-x[8], y[8], -z[8]), new Vector3(-x[9], -y[9], -z[9]) };
                                faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 2, 3 }, new int[] { 0, 3, 1 }, new int[] { 1, 4, 5 }, new int[] { 1, 5, 2 }, new int[] { 2, 5, 6 }, new int[] { 2, 6, 7 }, new int[] { 2, 7, 3 }, new int[] { 3, 7, 8 }, new int[] { 3, 8, 9 }, new int[] { 3, 9, 1 }, new int[] { 1, 9, 4 }, new int[] { 4, 5, 9 }, new int[] { 5, 6, 7 }, new int[] { 7, 8, 9 }, new int[] { 9, 5, 7 } };
                            }
                            else
                            {
                                if (shape == 6)
                                {
                                    verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(-x[4], y[4], z[4]), new Vector3(-x[5], -y[5], z[5]), new Vector3(-x[6], -y[6], -z[6]) };
                                    faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 2, 3, 4 }, new int[] { 0, 1, 6, 5 }, new int[] { 0, 4, 5 }, new int[] { 1, 2, 3, 6 }, new int[] { 3, 4, 5, 6 } };
                                }
                                else
                                {
                                    if (shape == 7)
                                    {
                                        verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(x[3], y[3], -z[3]), new Vector3(-x[4], y[4], -z[4]), new Vector3(-x[5], y[5], z[5]), new Vector3(-x[6], y[6], z[6]), new Vector3(-x[7], y[7], -z[7]), new Vector3(-x[8], -y[8], -z[8]), new Vector3(-x[9], -y[9], z[9]) };
                                        faces = new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 0, 2, 3 }, new int[] { 0, 5, 6 }, new int[] { 0, 6, 9 }, new int[] { 0, 1, 8, 9 }, new int[] { 0, 3, 4, 5 }, new int[] { 1, 2, 7, 8 }, new int[] { 2, 3, 4, 7 }, new int[] { 4, 5, 6, 7 }, new int[] { 6, 7, 8, 9 } };
                                    }
                                    else
                                    {
                                        if (shape == 8)
                                        {
                                            verts = new List<Vector3> { new Vector3(x[0], y[0], z[0]), new Vector3(x[1], -y[1], -z[1]), new Vector3(x[2], y[2], -z[2]), new Vector3(-x[3], y[3], -z[3]), new Vector3(-x[4], -y[4], -z[4]), new Vector3(-x[5], -y[5], z[5]), new Vector3(-x[6], y[6], z[6]) };
                                            faces = new List<int[]> { new int[] { 0, 2, 1 }, new int[] { 0, 1, 4 }, new int[] { 0, 4, 5 }, new int[] { 0, 5, 6 }, new int[] { 0, 6, 3, 2 }, new int[] { 2, 1, 4, 3 }, new int[] { 3, 6, 5, 4 } };
                                        }
                                        else
                                        {
                                            if (shape == 9)
                                            {
                                                verts = new List<Vector3> { new Vector3(-x[0], -y[0], -z[0]), new Vector3(-x[1], y[1], -z[1]), new Vector3(-x[2], y[2], z[2]), new Vector3(-x[3], -y[3], z[3]), new Vector3(x[4], -y[4], -z[4]), new Vector3(x[5], y[5], -z[5]), new Vector3(x[6], y[6], z[6]), new Vector3(x[7], -y[7], z[7]) };
                                                faces = new List<int[]> { new int[] { 0, 1, 6, 2 }, new int[] { 1, 5, 7, 6 }, new int[] { 5, 4, 3, 7 }, new int[] { 4, 0, 2, 3 }, new int[] { 0, 1, 5, 4 }, new int[] { 3, 2, 6, 7 } };
                                            }
                                            else
                                            {
                                                if (shape == 10)
                                                {
                                                    verts = new List<Vector3> { new Vector3(-x[0], -y[0], -z[0]), new Vector3(-x[1], y[1], -z[1]), new Vector3(-x[2], y[2], z[2]), new Vector3(x[3], -y[3], z[3]), new Vector3(x[4], y[4], z[4]), new Vector3(x[5], y[5], -z[5]), new Vector3(x[6], -y[6], -z[6]) };
                                                    faces = new List<int[]> { new int[] { 0, 2, 3 }, new int[] { 0, 3, 6 }, new int[] { 0, 1, 5, 6 }, new int[] { 2, 3, 4 }, new int[] { 0, 1, 2 }, new int[] { 1, 2, 4, 5 }, new int[] { 3, 4, 5, 6 } };
                                                }
                                                else
                                                {
                                                    if (shape == 11)
                                                    {
                                                        verts = new List<Vector3> { new Vector3(-x[0], -y[0], -z[0]), new Vector3(-x[1], y[1], -z[1]), new Vector3(-x[2], y[2], z[2]), new Vector3(x[3], -y[3], z[3]), new Vector3(x[4], y[4], z[4]), new Vector3(x[5], y[5], -z[5]), new Vector3(x[6], -y[6], -z[6]) };
                                                        faces = new List<int[]> { new int[] { 0, 2, 3 }, new int[] { 0, 3, 6 }, new int[] { 0, 1, 5, 6 }, new int[] { 2, 3, 4 }, new int[] { 5, 6, 3 }, new int[] { 1, 5, 3, 4 }, new int[] { 0, 1, 4, 2 } };
                                                    }
                                                    else
                                                    {
                                                        verts = new List<Vector3> { new Vector3(-x[0], -y[0], -z[0]), new Vector3(-x[1], y[1], -z[1]), new Vector3(-x[2], -y[2], z[2]), new Vector3(-x[3], y[3], z[3]), new Vector3(x[4], -y[4], -z[4]), new Vector3(x[5], y[5], -z[5]), new Vector3(x[6], -y[6], z[6]), new Vector3(x[7], y[7], z[7]) };
                                                        faces = new List<int[]> { new int[] { 0, 1, 3, 2 }, new int[] { 0, 1, 5, 4 }, new int[] { 0, 4, 6, 2 }, new int[] { 7, 5, 4, 6 }, new int[] { 7, 3, 2, 6 }, new int[] { 7, 5, 1, 3 } };
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var surface = new Surface(verts, faces);
            // Source indexes Blender's implicit edge order. Here edges use first face encounter order.
            // Approximation: crease locations may differ from Blender despite identical distributions.
            var edges = surface.Edges();
            var creases = new float[edges.Count];
            if (shape == 0)
            {
                for (int i = 0; i < 12; i++)
                {
                    creases[i] = rng.Gaussian(0.125f, 0.125f);
                }
            }
            else
            {
                if (shape == 1)
                {
                    foreach (int i in new int[] { 0, 2 })
                    {
                        creases[i] = rng.Gaussian(0.5f, 0.125f);
                    }
                    foreach (int i in new int[] { 6, 9, 11, 12 })
                    {
                        creases[i] = rng.Gaussian(0.25f, 0.05f);
                    }
                    foreach (int i in new int[] { 5, 7, 15, 16 })
                    {
                        creases[i] = rng.Gaussian(0.125f, 0.025f);
                    }
                }
                else
                {
                    if (shape == 2)
                    {
                        for (int i = 0; i < 18; i++)
                        {
                            creases[i] = rng.Gaussian(0.125f, 0.025f);
                        }
                    }
                    else
                    {
                        if (shape == 3)
                        {
                            foreach (int i in new int[] { 0, 1, 6, 10, 13 })
                            {
                                creases[i] = rng.Gaussian(0.25f, 0.05f);
                            }
                            creases[8] = rng.Gaussian(0.5f, 0.125f);
                        }
                        else
                        {
                            if (shape == 4)
                            {
                                foreach (int i in new int[] { 5, 6, 7, 10, 14, 16, 19, 21 })
                                {
                                    creases[i] = rng.Gaussian(0.5f, 0.125f);
                                }
                            }
                            else
                            {
                                if (shape == 7)
                                {
                                    for (int i = 0; i < 18; i++)
                                    {
                                        if ((i == 0 || i == 1 || i == 2 || i == 3 || i == 6 || i == 7 || i == 8 || i == 9 || i == 13 || i == 16))
                                        {
                                            creases[i] = rng.Gaussian(0.5f, 0.125f);
                                        }
                                        else
                                        {
                                            if ((i == 11 || i == 17))
                                            {
                                                creases[i] = rng.Gaussian(0.25f, 0.05f);
                                            }
                                            else
                                            {
                                                creases[i] = rng.Gaussian(0.125f, 0.025f);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (shape == 8)
                                    {
                                        for (int i = 0; i < 12; i++)
                                        {
                                            if ((i == 0 || i == 3 || i == 8 || i == 9 || i == 10))
                                            {
                                                creases[i] = rng.Gaussian(0.5f, 0.125f);
                                            }
                                            else
                                            {
                                                if (i == 11)
                                                {
                                                    creases[i] = rng.Gaussian(0.25f, 0.05f);
                                                }
                                                else
                                                {
                                                    creases[i] = rng.Gaussian(0.125f, 0.025f);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (shape == 9)
                                        {
                                            for (int i = 0; i < 12; i++)
                                            {
                                                if ((i == 0 || i == 3 || i == 4 || i == 11))
                                                {
                                                    creases[i] = rng.Gaussian(0.5f, 0.125f);
                                                }
                                                else
                                                {
                                                    creases[i] = rng.Gaussian(0.25f, 0.05f);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (shape == 10)
                                            {
                                                for (int i = 0; i < 12; i++)
                                                {
                                                    if ((i == 0 || i == 2 || i == 3 || i == 4 || i == 8 || i == 11))
                                                    {
                                                        creases[i] = rng.Gaussian(0.5f, 0.125f);
                                                    }
                                                    else
                                                    {
                                                        if ((i == 1 || i == 5 || i == 7))
                                                        {
                                                            creases[i] = rng.Gaussian(0.25f, 0.05f);
                                                        }
                                                        else
                                                        {
                                                            creases[i] = rng.Gaussian(0.125f, 0.025f);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (shape == 11)
                                                {
                                                    for (int i = 0; i < 11; i++)
                                                    {
                                                        if ((i == 1 || i == 2 || i == 3 || i == 4 || i == 8 || i == 11))
                                                        {
                                                            creases[i] = rng.Gaussian(0.25f, 0.05f);
                                                        }
                                                        else
                                                        {
                                                            creases[i] = rng.Gaussian(0.125f, 0.025f);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < edges.Count; i++) surface.Creases[Key(edges[i].A, edges[i].B)] = Mathf.Clamp01(creases[i]);
            surface.Orient();
            return surface;
        }
    }
}

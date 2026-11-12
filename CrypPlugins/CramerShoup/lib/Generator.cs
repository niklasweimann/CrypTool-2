/*
   Copyright (C) CrypTool 2 Team

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace CrypTool.Plugins.CramerShoup.lib
{
    public class ECCramerShoupGenerator
    {
        /// <summary>
        /// Curves that BouncyCastle 2 no longer lists: "curve25519" in short Weierstrass form, as BouncyCastle 1.x had it
        /// (y^2 = x^3 + a x + b over 2^255 - 19, derived from Curve25519 with x = u + 486662 / 3; base point u = 9).
        /// </summary>
        private static Org.BouncyCastle.Asn1.X9.X9ECParameters RemovedCurve(string name)
        {
            if (name != "curve25519")
            {
                return null;
            }
            BigInteger p = BigInteger.Two.Pow(255).Subtract(BigInteger.ValueOf(19));
            BigInteger a = new BigInteger("2AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA984914A144", 16);
            BigInteger b = new BigInteger("7B425ED097B425ED097B425ED097B425ED097B425ED097B4260B5E9C7710C864", 16);
            BigInteger n = new BigInteger("1000000000000000000000000000000014DEF9DEA2F79CD65812631A5CF5D3ED", 16);
            BigInteger h = BigInteger.ValueOf(8);
            Org.BouncyCastle.Math.EC.ECCurve curve = new Org.BouncyCastle.Math.EC.FpCurve(p, a, b, n, h);
            Org.BouncyCastle.Math.EC.ECPoint g = curve.CreatePoint(
                new BigInteger("2AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD245A", 16),
                new BigInteger("20AE19A1B8A086B4E01EDD2C7748D14C923D4D7E6D7C61B229E9C5A27ECED3D9", 16));
            return new Org.BouncyCastle.Asn1.X9.X9ECParameters(curve, new Org.BouncyCastle.Asn1.X9.X9ECPoint(g, false), n, h);
        }

        public ECCramerShoupKeyPair Generator(SecureRandom random, string customcurve)
        {
            Org.BouncyCastle.Asn1.X9.X9ECParameters parameter = CustomNamedCurves.GetByName(customcurve) ?? RemovedCurve(customcurve);
            Org.BouncyCastle.Math.EC.ECCurve curve = parameter.Curve;
            BigInteger p = parameter.N;
            BigInteger min = parameter.H;
            BigInteger max = p.Subtract(BigInteger.Two);
            BigInteger g1i = BigIntegers.CreateRandomInRange(min, max, random);
            Org.BouncyCastle.Math.EC.ECPoint g1 = parameter.G.Multiply(g1i);
            BigInteger g2i = BigIntegers.CreateRandomInRange(min, max, random);
            Org.BouncyCastle.Math.EC.ECPoint g2 = g1.Multiply(g2i);
            //    BigInteger p = BigInteger.ValueOf(23), g1 = BigInteger.ValueOf(7), g2 = BigInteger.ValueOf(13);


            BigInteger x1 = BigIntegers.CreateRandomInRange(min, max, random);
            BigInteger x2 = BigIntegers.CreateRandomInRange(min, max, random);
            BigInteger y1 = BigIntegers.CreateRandomInRange(min, max, random);
            BigInteger y2 = BigIntegers.CreateRandomInRange(min, max, random);
            BigInteger z = BigIntegers.CreateRandomInRange(min, max, random);

            Org.BouncyCastle.Math.EC.ECPoint c1 = g1.Multiply(x1);
            Org.BouncyCastle.Math.EC.ECPoint c = g2.Multiply(x2).Add(c1);
            Org.BouncyCastle.Math.EC.ECPoint d1 = g1.Multiply(y1);
            Org.BouncyCastle.Math.EC.ECPoint d = g2.Multiply(y2).Add(d1);
            Org.BouncyCastle.Math.EC.ECPoint h = g1.Multiply(z);

            Sha512Digest digest = new Sha512Digest();
            ECCramerShoupPrivateParameter priv = new ECCramerShoupPrivateParameter { Q = p, G1 = g1, G2 = g2, Digest = digest, X1 = x1, X2 = x2, Y1 = y1, Y2 = y2, Z = z };
            ECCramerShoupPublicParameter publ = new ECCramerShoupPublicParameter { Q = p, G1 = g1, G2 = g2, Digest = digest, H = h, C = c, D = d };

            return new ECCramerShoupKeyPair { Priv = priv, Public = publ };
        }
    }
}

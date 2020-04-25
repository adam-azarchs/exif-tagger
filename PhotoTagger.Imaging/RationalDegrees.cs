using System;
using System.Linq;
using System.Numerics;

namespace PhotoTagger.Imaging {
    /// <summary>
    /// Represents a rational number of degrees, minutes, and seconds.
    /// </summary>
    public struct RationalDegrees : IEquatable<RationalDegrees> {
        readonly UInt32 degreesN, minutesN, secondsN;
        readonly UInt32 degreesD, minutesD, secondsD;
        readonly int sign;

        public double ToDouble() {
            return this.toRational().ToDouble();
        }

        public Decimal ToDecimal() {
            return this.toRational().ToDecimal();
        }

        private Rational toRational() {
            return (new Rational(degreesN, degreesD) +
                    new Rational(minutesN, minutesD * 60L) +
                    new Rational(secondsN, secondsD * 3600L)) * sign;
        }

        private RationalDegrees(UInt32 degreesN, UInt32 minutesN, UInt32 secondsN, UInt32 degreesD, UInt32 minutesD, UInt32 secondsD, int sign) {
            this.degreesN = degreesN;
            this.minutesN = minutesN;
            this.secondsN = secondsN;
            this.degreesD = degreesD;
            this.minutesD = minutesD;
            this.secondsD = secondsD;
            this.sign = sign;
            if (degreesN == 0 &&
                minutesN == 0 &&
                secondsN == 0) {
                this.sign = 0;
            }
        }

        public static readonly RationalDegrees Zero = new RationalDegrees(0, 0, 0, 1, 1, 1, 1);

        public int Sign => sign;

        internal static RationalDegrees FromRational(Rational value) {
            int sign = value.Sign;
            if (sign == 0) {
                return Zero;
            } else if (sign == -1) {
                value *= -1;
            }
            UInt32 degrees = value.UInt32Floor();
            value -= degrees;
            if (value.IsZero) {
                return new RationalDegrees(degrees, 0, 0, 1, 1, 1, sign);
            }
            value *= 60;
            UInt32 minutes = value.UInt32Floor();
            value -= minutes;
            if (value.IsZero) {
                return new RationalDegrees(degrees, minutes, 0, 1, 1, 1, sign);
            }
            value *= 60;
            value.GetLowestForm(out UInt32 seconds, out UInt32 secondsDenom);
            return new RationalDegrees(degrees, minutes, seconds, 1, 1, secondsDenom, sign);
        }

        public static RationalDegrees FromDouble(double value) {
            return FromRational(Rational.FromDouble(value));
        }

        public static RationalDegrees FromDecimal(Decimal value) {
            return FromRational(Rational.FromDecimal(value));
        }

        public byte[] ToBytes() {
            return BitConverter.GetBytes(degreesN).Concat(
                BitConverter.GetBytes(degreesD)).Concat(
                BitConverter.GetBytes(minutesN)).Concat(
                BitConverter.GetBytes(minutesD)).Concat(
                BitConverter.GetBytes(secondsN)).Concat(
                BitConverter.GetBytes(secondsD)).ToArray();
        }

        public static RationalDegrees FromBytes(byte[] bytes, int sign) {
            return new RationalDegrees(
                BitConverter.ToUInt32(bytes, 0),
                BitConverter.ToUInt32(bytes, 2 * sizeof(uint)),
                BitConverter.ToUInt32(bytes, 4 * sizeof(uint)),
                BitConverter.ToUInt32(bytes, 1 * sizeof(uint)),
                BitConverter.ToUInt32(bytes, 3 * sizeof(uint)),
                BitConverter.ToUInt32(bytes, 5 * sizeof(uint)),
                sign);
        }

        public override bool Equals(object? obj) {
            if (obj is RationalDegrees other) {
                return this.Equals(other);
            } else {
                return false;
            }
        }

        public bool Equals(RationalDegrees other) {
            if (this.sign != other.sign) {
                return false;
            }
            if (this.degreesN == other.degreesN &&
                this.degreesD == other.degreesD &&
                this.minutesN == other.minutesN &&
                this.minutesD == other.minutesD &&
                this.secondsN == other.secondsN &&
                this.secondsD == other.secondsD) {
                return true;
            }
            return this.toRational().Equals(other.toRational());
        }

        public static bool operator ==(RationalDegrees d1, RationalDegrees d2) {
            return d1.Equals(d2);
        }

        public static bool operator !=(RationalDegrees d1, RationalDegrees d2) {
            return !d1.Equals(d2);
        }

        public override int GetHashCode() {
            return this.toRational().GetHashCode();
        }
    }

    internal struct Rational : IEquatable<Rational> {
        readonly BigInteger numerator, denominator;

        public Rational(BigInteger numerator, BigInteger denominator) {
            this.numerator = numerator;
            this.denominator = denominator;
        }

        public int Sign {
            get {
                if (numerator == 0) {
                    return 0;
                } else if (numerator > 0) {
                    return 1;
                } else {
                    return -1;
                }
            }
        }

        public bool IsZero => numerator == 0;

        public static readonly Rational Zero = new Rational(0, 1);

        public static Rational FromDouble(double value) {
            if (double.IsNaN(value)) {
                throw new ArgumentException("Not a number.", nameof(value));
            } else if (double.IsInfinity(value)) {
                throw new ArgumentException("Not finite.", nameof(value));
            } else if (value == 0) {
                return Zero;
            }
            var sign = (value > 0) ? 1 : -1;
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
            int exponent = (int)(bits >> 52) & 0x7FF;
            long mantissa = (long)(bits & 0x000FFFFFFFFFFFFF);
            if (exponent == 0) {
                mantissa |= 0x0010000000000000;
            }
            if (exponent == 0) {
                exponent = -1074;
            } else {
                exponent -= 1075;
            }

            BigInteger numerator;
            BigInteger denominator;
            if (exponent >= 0) {
                numerator = mantissa *
                  BigInteger.Pow(2, exponent);
                denominator = 1;
            } else {
                numerator = mantissa;
                denominator = BigInteger.Pow(2, -exponent);
            }
            return new Rational(sign * numerator, denominator);
        }

        public static Rational FromDecimal(Decimal value) {
            BigInteger sign = 1;
            if (value < 0) {
                sign = -1;
                value = -value;
            }
            BigInteger numerator = new BigInteger(Decimal.Floor(value));
            BigInteger denominator = 1;
            value -= Decimal.Floor(value);
            while (value != 0) {
                numerator *= 10;
                denominator *= 10;
                value *= 10;
                numerator += new BigInteger(Decimal.Floor(value));
                value -= Decimal.Floor(value);
            }
            return new Rational(sign * numerator, denominator);
        }

        public Decimal ToDecimal() {
            var gcd = BigInteger.GreatestCommonDivisor(this.numerator, this.denominator);
            var numerator = this.numerator / gcd;
            var denominator = this.denominator / gcd;
            return (Decimal)numerator / (Decimal)denominator;
        }

        public double ToDouble() {
            var gcd = BigInteger.GreatestCommonDivisor(this.numerator, this.denominator);
            var numerator = this.numerator / gcd;
            var denominator = this.denominator / gcd;
            return (double)numerator / (double)denominator;
        }

        public static bool TryParse(string value, out Rational parsed) {
            if (!Decimal.TryParse(value, out decimal d)) {
                parsed = Rational.Zero;
                return false;
            }
            parsed = Rational.FromDecimal(d);
            return true;
        }

        internal UInt32 UInt32Floor() {
            return checked((UInt32)BigInteger.Divide(numerator, denominator));
        }

        public void GetLowestForm(out uint lowestNum, out uint lowestDen) {
            if (this.numerator < 0) {
                throw new OverflowException();
            } else if (this.numerator == 0) {
                lowestNum = 0;
                lowestDen = 1;
                return;
            }
            var gcd = BigInteger.GreatestCommonDivisor(this.numerator, this.denominator);
            var numerator = this.numerator / gcd;
            var denominator = this.denominator / gcd;
            if (denominator < uint.MaxValue && numerator < uint.MaxValue) {
                lowestNum = checked((uint)numerator);
                lowestDen = checked((uint)denominator);
                return;
            }
            var whole = BigInteger.DivRem(numerator, denominator, out numerator);
            BigInteger lbNum = 0, lbDen = 1, ubNum = 1, ubDen = 1, lastMedNum = 0, lastMedDen = 1;
            uint iters = 0;
            while ((numerator + whole * denominator) > uint.MaxValue || denominator > uint.MaxValue) {
                if (denominator == 1) {
                    throw new OverflowException();
                }
                var medNum = lbNum + ubNum;
                var medDen = lbDen + ubDen;
                gcd = BigInteger.GreatestCommonDivisor(medNum, medDen);
                medNum /= gcd;
                medDen /= gcd;
                if (medDen > uint.MaxValue || medNum + whole * medDen > uint.MaxValue) {
                    break;
                }
                lastMedNum = medNum;
                lastMedDen = medDen;
                if (numerator * medDen < medNum * denominator) {
                    ubNum = medNum;
                    ubDen = medDen;
                } else {
                    lbNum = medNum;
                    lbDen = medDen;
                }
                if (++iters > 10000) {
                    break;
                }
            }
            lowestNum = checked((uint)(lastMedNum + whole * lastMedDen));
            lowestDen = checked((uint)lastMedDen);
        }

        public static Rational operator +(Rational lhs, Rational rhs) {
            BigInteger numerator = lhs.numerator * rhs.denominator + rhs.numerator * lhs.denominator;
            BigInteger denominator = lhs.denominator * rhs.denominator;
            if (numerator == 0) {
                return Zero;
            }
            if (denominator == 0) {
                throw new DivideByZeroException();
            }
            return new Rational(numerator, denominator);
        }

        public static Rational operator -(Rational lhs, Rational rhs) {
            BigInteger numerator = lhs.numerator * rhs.denominator - rhs.numerator * lhs.denominator;
            BigInteger denominator = lhs.denominator * rhs.denominator;
            if (numerator == 0) {
                return Zero;
            }
            if (denominator == 0) {
                throw new DivideByZeroException();
            }
            return new Rational(numerator, denominator);
        }

        public static Rational operator -(Rational lhs, UInt32 rhs) {
            return new Rational(lhs.numerator - (rhs * lhs.denominator),
                lhs.denominator);
        }

        public static Rational operator *(Rational lhs, int rhs) {
            return new Rational(lhs.numerator * rhs, lhs.denominator);
        }

        public override bool Equals(object? obj) {
            if (obj is Rational other) {
                return this.Equals(other);
            } else {
                return false;
            }
        }

        public bool Equals(Rational other) {
            if (this.numerator == 0) {
                return other.numerator == 0;
            }
            if (other.numerator == 0) {
                return false;
            }
            if (this.denominator == 0) {
                return other.denominator == 0;
            }
            if (other.denominator == 0) {
                return false;
            }
            if (this.numerator == other.numerator &&
                this.denominator == other.denominator) {
                return true;
            }
            var thisGCD = BigInteger.GreatestCommonDivisor(numerator, denominator);
            var otherGCD = BigInteger.GreatestCommonDivisor(other.numerator, other.denominator);
            if (numerator / thisGCD != other.numerator / otherGCD) {
                return false;
            }
            return this.denominator / thisGCD == other.denominator / otherGCD;
        }

        public override int GetHashCode() {
            if (numerator == 0 || denominator == 0) {
                return 0;
            }
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            return (numerator / gcd).GetHashCode() ^ (denominator / gcd).GetHashCode();
        }
    }
}

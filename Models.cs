using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Numerics;

namespace PhoFit {
public class Spectrum {
    public double[] X, Y;
    public static double Number(string s) {
        double n = double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        if (double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("Non-finite number");
        return n;
    }
    public static Spectrum Read(string file) {
        var x = new List<double>(); var y = new List<double>();
        int columns = 0, line = 0;
        foreach (string raw in File.ReadLines(file)) {
            line++;
            string row = raw.Split('#')[0].Trim();
            if (row.Length == 0 || row.StartsWith("//")) continue;
            var fields = Regex.Split(row, @"\s*[,;]\s*|\s+");
            if (columns == 0 && Regex.IsMatch(row, @"^(channel|energy|kenh|x|counts|count|dem)(\b|[,;])", RegexOptions.IgnoreCase)) continue;
            if (columns == 0) columns = fields.Length;
            try {
                if (columns > 2 || fields.Length != columns) throw new FormatException();
                double axis = columns == 1 ? y.Count : Number(fields[0]);
                if (x.Count > 0 && axis <= x.Last()) throw new FormatException("X must increase");
                x.Add(axis); y.Add(Number(fields[columns - 1]));
            } catch (FormatException e) { throw new FormatException(file + " / line " + line + ": invalid spectrum row. " + e.Message); }
        }
        if (x.Count < 2) throw new FormatException("At least two data points are required.");
        return new Spectrum { X = x.ToArray(), Y = y.ToArray() };
    }
}

public class Parameter {
    public string Name, Link = "";
    public double Value, Min, Max;
    public bool Fixed;
    public Parameter() {}
    public Parameter(string name, double value, double min = double.NegativeInfinity, double max = double.PositiveInfinity, bool fix = false) {
        Name = name; Value = value; Min = min; Max = max; Fixed = fix;
    }
}
public class Component {
    public string Kind;
    public List<Parameter> P = new List<Parameter>();
    public bool Background { get { return Kind == "POLY" || Kind == "EXPO" || Kind == "STEP" || Kind == "DSTEP"; } }
    public static readonly string[] Kinds = ("G TG DTG ALPHA ALPHA3 V TV DTV L GS VS AS A3S HYPER HYPERV LOWE LOWEV POLY EXPO STEP DSTEP BETA1 BETA2 BETA3 BETA4 BETA5 BETA6 BETA7").Split(' ');
    bool Voigt { get { return Kind == "V" || Kind == "TV" || Kind == "DTV" || Kind == "VS" || Kind == "HYPERV" || Kind == "LOWEV"; } }
    bool Hyper { get { return Kind.StartsWith("HYPER") || Kind.StartsWith("LOWE"); } }
    bool LinkedWidth { get { return Kind == "GS" || Kind == "VS" || Kind == "AS" || Kind == "A3S"; } }
    int Tails {
        get {
            if (Kind == "ALPHA3" || Kind == "A3S") return 3;
            if (Kind == "ALPHA" || Kind == "AS" || Kind == "DTG" || Kind == "DTV") return 2;
            return Kind == "TG" || Kind == "TV" || Hyper ? 1 : 0;
        }
    }
    void Add(string n, double v, double lo = double.NegativeInfinity, double hi = double.PositiveInfinity, bool fix = false) {
        P.Add(new Parameter(n, v, lo, hi, fix));
    }
    public static Component Create(string kind, double x, double y, double step, double left, double right) {
        var c = new Component { Kind = kind };
        double w = Math.Max(step, (right - left) / 30), min = step / 100;
        if (kind == "POLY") {
            for (int i = 0; i <= 5; i++) c.Add("c" + i, i == 0 ? y : 0, fix: i > 0);
        } else if (kind == "EXPO") {
            c.Add("A", Math.Max(0, y), 0); c.Add("decay", 1, 0); c.Add("offset", 0, fix: true);
        } else if (c.Background) {
            c.Add("offset", 0); c.Add("step", Math.Max(0, y), 0);
            c.Add("center", x, left, right); c.Add("sigma", w, min);
            if (kind == "DSTEP") { c.Add("step2", y / 2); c.Add("center2", (x + right) / 2, left, right); c.Add("sigma2", w, min); }
        } else if (kind.StartsWith("BETA")) {
            c.Add("A", Math.Max(1, y), 0); c.Add("endpoint_keV", Math.Max(step, x), min);
            c.Add("Z", 1, 1, 118, true); c.Add("mass", 3, 1, 300, true);
            c.Add("charge", -1, -1, 1, true); c.Add("screening", 0, 0, 1, true);
            foreach (string n in new[]{"a","b","c","d","A_shape","B_shape","lambda1","lambda2","lambda3"})
                c.Add(n, n.Length == 1 ? 0 : 1, fix: true);
        } else {
            c.Add("A", Math.Max(1, y), 0); c.Add("center", x, left, right); c.Add(kind == "L" ? "gamma_HWHM" : "sigma", w, min);
            if (c.LinkedWidth) {
                c.P[2].Link = "sigma_" + kind;
                if (kind == "GS" || kind == "VS") {
                    c.Add("k", 0, fix: true); c.P.Last().Link = "slope_" + kind;
                }
            }
            if (c.Voigt) c.Add("gamma_HWHM", w / 5, 0);
            for (int i = 1; i <= c.Tails; i++) {
                c.Add("tail" + i, 0.05 / i, 0); c.Add("tau" + i, w * i, min);
            }
            if (c.Hyper) c.Add("step_ratio", 0.01, 0);
            if (kind.StartsWith("LOWE")) { c.Add("shelf_ratio", 0.005, 0); c.Add("k_edge", 0.1, 0, 1); }
        }
        return c;
    }
    static double ErfcFactor(double x) {
        double t = 1 / (1 + 0.3275911 * x);
        return (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t;
    }
    static double Erfc(double x) {
        double a = Math.Abs(x), v = ErfcFactor(a) * Math.Exp(-a * a);
        return x < 0 ? 2 - v : v;
    }
    static double Tail(double d, double w, double tau) {
        double z = (d / w + w / tau) / Math.Sqrt(2);
        return 0.5 * (z >= 0
            ? Math.Exp(-0.5 * (d / w) * (d / w)) * ErfcFactor(z)
            : Math.Exp(d / tau + 0.5 * (w / tau) * (w / tau)) * Erfc(z));
    }
    // Unit-height pseudo-Voigt using the Thompson-Cox-Hastings width.
    static double Profile(double d, double sigma, double gamma) {
        if (gamma == 0) return Math.Exp(-0.5 * (d / sigma) * (d / sigma));
        double g = 2.354820045 * sigma, l = 2 * gamma, sum = g + l;
        g /= sum; l /= sum;
        double width = sum * Math.Pow(Math.Pow(g, 5) + 2.69269 * Math.Pow(g, 4) * l +
            2.42843 * Math.Pow(g, 3) * l * l + 4.47163 * g * g * Math.Pow(l, 3) +
            0.07842 * g * Math.Pow(l, 4) + Math.Pow(l, 5), 0.2);
        double r = 2 * gamma / width, eta = r * (1.36603 + r * (-0.47719 + 0.11116 * r));
        // Convert the area-normalized mixing weight to a height weight.
        double h = eta * 2 / Math.PI, gHeight = (1 - eta) * Math.Sqrt(4 * Math.Log(2) / Math.PI);
        double u = 2 * d / width;
        return (h / (1 + u * u) + gHeight * Math.Exp(-Math.Log(2) * u * u)) / (h + gHeight);
    }
    double V(int i) { return P[i].Value; }
    public double Eval(double x, double origin, double span) {
        double u = (x - origin) / span;
        if (Kind == "POLY") {
            double y = 0; for (int i = P.Count - 1; i >= 0; i--) y = y * u + V(i);
            return y;
        }
        if (Kind == "EXPO") return V(0) * Math.Exp(-V(1) * u) + V(2);
        if (Background) return V(0) + V(1) * Erfc((x - V(2)) / (Math.Sqrt(2) * V(3))) / 2 +
            (Kind == "DSTEP" ? V(4) * Erfc((x - V(5)) / (Math.Sqrt(2) * V(6))) / 2 : 0);
        if (Kind.StartsWith("BETA")) return Beta(x);
        double d = x - V(1), w = V(2);
        if (Kind == "L") return V(0) / (1 + (d / w) * (d / w));
        int index = 3;
        if (Kind == "GS" || Kind == "VS") w = Math.Sqrt(w * w + V(index++) * V(1));
        double gamma = Voigt ? V(index++) : 0;
        double value = Profile(d, w, gamma);
        for (int i = 0; i < Tails; i++) {
            bool right = i == 1 && (Kind == "DTG" || Kind == "DTV");
            double ratio = V(index++), tau = V(index++);
            value += ratio * Tail(right ? -d : d, w, tau);
        }
        if (Hyper) value += V(index++) * Erfc(d / (w * Math.Sqrt(2))) / 2;
        if (Kind.StartsWith("LOWE")) value += V(index) *
            (Erfc(d / (w * Math.Sqrt(2))) - Erfc((x - V(index + 1) * V(1)) / (w * Math.Sqrt(2)))) / 2;
        return V(0) * value;
    }
    static Complex LogGamma(Complex z) {
        double[] coeff = {676.5203681218851,-1259.1392167224028,771.3234287776531,-176.6150291621406,
            12.5073432786869,-0.1385710952657201,9.98436957801957e-6,1.50563273514931e-7};
        z -= 1; Complex sum = 0.99999999999980993;
        for (int i = 0; i < coeff.Length; i++) sum += coeff[i] / (z + i + 1);
        Complex t = z + 7.5;
        return 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Complex.Log(t) - t + Complex.Log(sum);
    }
    double Beta(double x) {
        if (x <= 0 || x >= V(1)) return 0;
        double e = 1 + x / 510.99895, q = (V(1) - x) / 510.99895, p2 = (e - 1) * (e + 1);
        double az = V(2) / 137.036, g = Math.Sqrt(1 - az * az);
        double es = e + (V(5) >= 0.5 ? V(4) * 1.45 * Math.Pow(V(2), 4.0 / 3) / (137.036 * 137.036) : 0);
        if (es <= 1) es = e;
        double ps = Math.Sqrt((es - 1) * (es + 1)), nu = az * es / ps, radius = Math.Pow(V(3), 1.0 / 3);
        radius = 0.002908 * radius - 0.002437 / radius;
        double logF = Math.Log(2 * (1 + g)) + 2 * (g - 1) * Math.Log(2 * ps * radius) -
            V(4) * Math.PI * nu + 2 * (LogGamma(new Complex(g, nu)).Real - LogGamma(2 * g + 1).Real);
        double q2 = q * q, baseShape = 1 + V(6) * e + V(7) / e + V(8) * e * e;
        double first = q2 + V(12) * p2, shape;
        switch (Kind) {
            case "BETA2": shape = baseShape + V(9) * first; break;
            case "BETA3": shape = first; break;
            case "BETA4": shape = q2 + V(11) * V(12) * p2; break;
            case "BETA5": shape = q2*q2 + (10.0/3) * V(12)*q2*p2 + V(13)*p2*p2; break;
            case "BETA6": shape = q2*q2 + V(10)*V(12)*q2*p2 + V(11)*V(13)*p2*p2; break;
            case "BETA7": shape = q2*q2*q2 + 7*V(12)*q2*q2*p2 + 7*V(13)*q2*p2*p2 + V(14)*p2*p2*p2; break;
            default: shape = baseShape; break;
        }
        return V(0) * ps * es * q2 * Math.Exp(logF) * shape;
    }
}
}

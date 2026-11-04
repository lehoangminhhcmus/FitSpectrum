using System;
using System.Linq;
using System.Collections.Generic;

namespace PhoFit {
public class Region {
    public double Left, Right;
    public int Method = 1;
    public List<Component> Components = new List<Component>();
}
public class Project {
    public Spectrum Data;
    public List<Region> Regions = new List<Region>();
}
public class FitResult {
    public string Warning="";
    public double Cost, RSquared, ChiSquared;
    public double ReducedChiSquared { get { return ChiSquared / Dof; } }
    public int Dof, Iterations;
    public bool Converged, Covariance;
    public double[] Areas, AreaErrors, Model, Residual;
    public Dictionary<Parameter,double> Errors = new Dictionary<Parameter,double>();
}
public class Fitter {
    class Group {
        public Parameter[] Members;
        public double Low, High, Value;
        public bool Fixed;
        public void Set(double value) {
            Value = Math.Max(Low, Math.Min(High, value));
            foreach (var p in Members) p.Value = Value;
        }
    }
    readonly Region roi;
    readonly Spectrum data;
    readonly int[] rows;
    readonly Group[] free;
    readonly double span;
    readonly double sampleStep;
    public Fitter(Spectrum spectrum, Region region) {
        data = spectrum; roi = region; span = roi.Right - roi.Left;
        if (!(span > 0) || roi.Components.Count == 0) throw new ArgumentException("ROI bounds or model invalid.");
        rows = Enumerable.Range(0, data.X.Length).Where(i => data.X[i] >= roi.Left && data.X[i] <= roi.Right).ToArray();
        if (roi.Method == 2 && rows.Any(i => data.Y[i] < 0)) throw new ArgumentException("Poisson requires nonnegative counts.");
        double[] spacings=rows.Skip(1).Select((i,j)=>data.X[i]-data.X[rows[j]]).OrderBy(v=>v).ToArray();
        sampleStep=spacings.Length==0?0:spacings[spacings.Length/2];
        foreach(var c in roi.Components.Where(c=>!c.Background && !c.Kind.StartsWith("BETA"))) {
            var width=c.P.FirstOrDefault(p=>p.Name=="sigma" || p.Name=="gamma_HWHM");
            if(width!=null && !width.Fixed && width.Value<sampleStep/2)
                throw new ArgumentException("Độ rộng "+c.Kind+" quá nhỏ so với bước đo. Bấm Ước lượng độ rộng hoặc tăng sigma/gamma trước khi khớp lại.");
        }
        var parameters = roi.Components.SelectMany(c => c.P).ToArray();
        var groups = parameters.GroupBy(p => string.IsNullOrWhiteSpace(p.Link) ? "#" + Array.IndexOf(parameters,p) : "@" + p.Link.Trim());
        var variables = new List<Group>();
        foreach (var items in groups) {
            var g = new Group { Members = items.ToArray(), Low = items.Max(p => p.Min), High = items.Min(p => p.Max) };
            var fixedValues = items.Where(p => p.Fixed).Select(p => p.Value).Distinct().ToArray();
            if (g.Low > g.High || fixedValues.Length > 1 ||
                (fixedValues.Length == 1 && (fixedValues[0] < g.Low || fixedValues[0] > g.High)))
                throw new ArgumentException("Inconsistent bounds or fixed values: " + items.Key);
            g.Fixed = g.Low == g.High || fixedValues.Length == 1;
            g.Set(fixedValues.Length == 1 ? fixedValues[0] : items.First().Value);
            if (!g.Fixed) variables.Add(g);
        }
        free = variables.ToArray();
        if (rows.Length <= free.Length || rows.Length < 2) throw new ArgumentException("ROI needs more samples than free parameters.");
    }
    double[] Residuals(double[] model) {
        var residual = new double[rows.Length];
        for (int j = 0; j < rows.Length; j++) {
            double x = data.X[rows[j]], y = data.Y[rows[j]];
            double f = roi.Components.Sum(c => c.Eval(x, roi.Left, span));
            if (model != null) model[j] = f;
            double d = y - f;
            if (roi.Method == 1) d /= Math.Sqrt(Math.Max(1, y));
            if (roi.Method == 2) {
                if (f < 0 || (f == 0 && y > 0)) d = double.NaN;
                else if (y == 0) d = -Math.Sqrt(2 * f);
                else {
                    double u = (f-y)/y;
                    double half = Math.Abs(u) < 1e-4 ? y*u*u*(0.5-u/3+u*u/4) : f-y+y*(Math.Log(y)-Math.Log(f));
                    d = Math.Sign(d) * Math.Sqrt(Math.Max(0, 2 * half));
                }
            }
            residual[j] = d;
        }
        return residual;
    }
    static double Cost(double[] r) { return r.Sum(v => v*v); }
    static bool Valid(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    double[][] Jacobian(double[] baseline, bool areas) {
        var jac = new double[free.Length][];
        for (int j = 0; j < free.Length; j++) {
            var p = free[j]; double v = p.Value, h = 6e-6 * Math.Max(Math.Abs(v), 1e-3);
            double hi = Math.Min(p.High, v+h), lo = Math.Max(p.Low, v-h);
            p.Set(hi); var a = areas ? Areas() : Residuals(null);
            p.Set(lo); var b = areas ? Areas() : Residuals(null);
            p.Set(v);
            jac[j] = new double[baseline.Length];
            bool aValid = a.All(Valid), bValid = b.All(Valid);
            for (int i = 0; i < baseline.Length; i++) {
                if (aValid && bValid && hi > lo) jac[j][i] = (a[i]-b[i])/(hi-lo);
                else if (aValid && hi > v) jac[j][i] = (a[i]-baseline[i])/(hi-v);
                else if (bValid && v > lo) jac[j][i] = (baseline[i]-b[i])/(v-lo);
            }
        }
        return jac;
    }
    static double Dot(double[] a, double[] b) {
        double sum = 0; for (int i=0; i<a.Length; i++) sum += a[i]*b[i]; return sum;
    }
    static double[,] Normal(double[][] jac, out double[] scale) {
        int n = jac.Length; var m = new double[n,n]; scale = new double[n];
        for (int i=0; i<n; i++) scale[i] = Math.Max(1e-100, Math.Sqrt(Dot(jac[i],jac[i])));
        for (int i=0; i<n; i++) for (int j=0; j<=i; j++)
            m[i,j] = m[j,i] = Dot(jac[i],jac[j])/scale[i]/scale[j];
        return m;
    }
    static double[] Solve(double[,] matrix, double[] rhs) {
        int n=rhs.Length; var a=(double[,])matrix.Clone(); var b=(double[])rhs.Clone();
        for (int j=0; j<n; j++) {
            int pivot=j;
            for (int i=j+1; i<n; i++) if (Math.Abs(a[i,j])>Math.Abs(a[pivot,j])) pivot=i;
            if (!Valid(a[pivot,j]) || Math.Abs(a[pivot,j])<1e-12) return null;
            for (int k=j; k<n; k++) { double t=a[j,k]; a[j,k]=a[pivot,k]; a[pivot,k]=t; }
            double tmp=b[j]; b[j]=b[pivot]; b[pivot]=tmp;
            for (int i=j+1; i<n; i++) {
                double f=a[i,j]/a[j,j];
                for (int k=j+1; k<n; k++) a[i,k]-=f*a[j,k];
                b[i]-=f*b[j];
            }
        }
        for (int i=n-1; i>=0; i--) {
            for (int k=i+1; k<n; k++) b[i]-=a[i,k]*b[k];
            b[i]/=a[i,i];
        }
        return b;
    }
    double[] Areas() {
        // Counts per measured bin, evaluated at the observed coordinates.
        return roi.Components.Select(c => rows.Sum(i => c.Eval(data.X[i],roi.Left,span))).ToArray();
    }
    public FitResult Run(int maxIterations) {
        var r=Residuals(null); double cost=Cost(r), damping=1e-3;
        if (!Valid(cost)) throw new ArgumentException("Initial model is invalid in the ROI.");
        int n=free.Length;
        var result=new FitResult { Dof=rows.Length-n, Converged=n==0 };
        for (int it=0; n>0 && it<maxIterations; it++) {
            result.Iterations=it+1;
            var jac=Jacobian(r,false); double[] scale;
            var matrix=Normal(jac,out scale); var rhs=new double[n];
            for (int i=0; i<n; i++) {
                rhs[i]=-Dot(jac[i],r)/scale[i];
                if ((free[i].Value==free[i].Low && rhs[i]<0) || (free[i].Value==free[i].High && rhs[i]>0)) {
                    for (int j=0;j<n;j++) matrix[i,j]=matrix[j,i]=0;
                    rhs[i]=0;
                }
                matrix[i,i]+=damping;
            }
            if (rhs.All(v => Math.Abs(v)<1e-8*(1+Math.Sqrt(cost)))) { result.Converged=true; break; }
            var step=Solve(matrix,rhs);
            if (step==null) break;
            var old=free.Select(p => p.Value).ToArray();
            // Keep positive profile widths away from a single step onto the lower bound.
            double fraction=1;
            for(int i=0;i<n;i++) if(free[i].Members.Any(p=>p.Name=="sigma" || p.Name=="gamma_HWHM" || p.Name.StartsWith("tau"))) {
                double delta=step[i]/scale[i];
                if(old[i]>0 && Math.Abs(delta)>old[i]*0.5)fraction=Math.Min(fraction,old[i]*0.5/Math.Abs(delta));
            }
            for (int i=0;i<n;i++) free[i].Set(old[i]+fraction*step[i]/scale[i]);
            var trial=Residuals(null); double next=Cost(trial);
            if (Valid(next) && next<cost) {
                double improvement=cost-next;
                cost=next; r=trial; damping=Math.Max(1e-12,damping/3);
                if (improvement<1e-10*(1+cost)) { result.Converged=true; break; }
            } else {
                for (int i=0;i<n;i++) free[i].Set(old[i]);
                damping*=10;
                if (damping>1e14) break;
            }
        }
        if(roi.Components.Where(c=>!c.Background && !c.Kind.StartsWith("BETA")).Any(c=>
            c.P.Any(p=>!p.Fixed && (p.Name=="sigma" || p.Name=="gamma_HWHM") && p.Value<sampleStep/2))) {
            result.Converged=false;result.Warning="Độ rộng nhỏ hơn nửa bước đo: đỉnh không được lấy mẫu đủ. Hãy kiểm tra mô hình và ước lượng lại độ rộng.";
        }
        result.Cost=cost; result.Model=new double[rows.Length];
        result.Residual=Residuals(result.Model);
        double mean=rows.Average(i=>data.Y[i]), sse=0, sst=0, chi=0;
        for(int j=0;j<rows.Length;j++) {
            double y=data.Y[rows[j]], d=y-result.Model[j];
            sse+=d*d; sst+=(y-mean)*(y-mean); chi+=d*d/Math.Max(1,y);
        }
        result.RSquared=sst>0 ? 1-sse/sst : double.NaN;
        result.ChiSquared=chi;
        result.Areas=Areas(); result.AreaErrors=Enumerable.Repeat(double.NaN,result.Areas.Length).ToArray();
        foreach (var p in roi.Components.SelectMany(c => c.P)) result.Errors[p]=0;
        foreach (var g in free) foreach (var p in g.Members) result.Errors[p]=double.NaN;
        double[] s;
        var normal=Normal(Jacobian(r,false),out s); var inv=new double[n][];
        for (int i=0;i<n;i++) { var unit=new double[n]; unit[i]=1; inv[i]=Solve(normal,unit); if (inv[i]==null) return result; }
        result.Covariance=true;
        double variance=roi.Method==0 ? cost/result.Dof : 1;
        for (int i=0;i<n;i++) foreach (var p in free[i].Members)
            result.Errors[p]=Math.Sqrt(Math.Max(0, inv[i][i]*variance))/s[i];
        var areaJac=Jacobian(result.Areas,true);
        for (int k=0;k<result.Areas.Length;k++) {
            double v=0;
            for (int i=0;i<n;i++) for (int j=0;j<n;j++)
                v+=areaJac[i][k]/s[i]*inv[i][j]*areaJac[j][k]/s[j];
            result.AreaErrors[k]=Math.Sqrt(Math.Max(0,v*variance));
        }
        return result;
    }
}
}

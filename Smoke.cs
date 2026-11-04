using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml.Serialization;
using PhoFit;
class Smoke {
    static void Check(bool b, string what) { if(!b) throw new Exception(what); }
    static void Main() {
        var x=Enumerable.Range(0,201).Select(i=>(double)i).ToArray();
        var y=x.Select(v=>8+200*Math.Exp(-Math.Pow((v-95)/5,2)/2)+120*Math.Exp(-Math.Pow((v-104)/4,2)/2)).ToArray();
        Func<double,string> n=v=>v.ToString("G17",CultureInfo.InvariantCulture);
        File.WriteAllLines("outputs/demo-counts.txt",y.Select(n));
        File.WriteAllLines("outputs/demo-energy.csv",new[]{"energy,counts"}.Concat(x.Select((v,i)=>n(v/2+20)+","+n(y[i]))));
        var s=Spectrum.Read("outputs/demo-counts.txt");
        var energy=Spectrum.Read("outputs/demo-energy.csv");
        Check(s.Y.SequenceEqual(energy.Y) && energy.X[0]==20 && s.X[200]==200,"read formats");
        foreach(int method in new[]{0,1,2}) {
            var r=new Region { Left=60,Right=145,Method=method };
            r.Components.Add(Component.Create("POLY",90,10,1,60,145));
            r.Components.Add(Component.Create("G",94,180,1,60,145));
            r.Components.Add(Component.Create("G",106,105,1,60,145));
            var f=new Fitter(s,r).Run(300);
            Console.WriteLine("Method {0}: converged={1}, objective={2:G5}, centers={3:G9} {4:G9}",method,f.Converged,f.Cost,r.Components[1].P[1].Value,r.Components[2].P[1].Value);
            Check(Math.Abs(f.RSquared-1)<1e-10 && f.ChiSquared<1e-7 && f.ReducedChiSquared==f.ChiSquared/f.Dof,"fit statistics");
            Check(f.Converged && f.Covariance && f.Cost<1e-7 && Math.Abs(r.Components[1].P[1].Value-95)<0.001 &&
                Math.Abs(r.Components[2].P[1].Value-104)<0.001 && f.Areas[1]>2500 && f.Areas[1]<2510,"fit");
            var p=new Project { Data=s };p.Regions.Add(r);
            var ser=new XmlSerializer(typeof(Project));
            using(var stream=new MemoryStream()) {
                ser.Serialize(stream,p);stream.Position=0;
                var read=(Project)ser.Deserialize(stream);
                Check(read.Data.Y.SequenceEqual(y)&&read.Regions[0].Components.Count==3,"project");
            }
        }
        foreach(string k in Component.Kinds) {
            var c=Component.Create(k,100,100,1,0,200);
            Check(x.All(v=>{var a=c.Eval(v,0,200);return !double.IsNaN(a)&&!double.IsInfinity(a);}),k);
        }
        // Known fixed model: y=[1,2,4], model=[2,2,2], SSE=5, SST=14/3.
        foreach(int method in new[]{0,1,2}) {
            var r=new Region { Left=0,Right=2,Method=method };
            var c=Component.Create("POLY",0,2,1,0,2); c.P[0].Fixed=true; r.Components.Add(c);
            var f=new Fitter(new Spectrum { X=new double[]{0,1,2},Y=new double[]{1,2,4} },r).Run(10);
            Check(Math.Abs(f.RSquared-(1-15.0/14))<1e-12,"analytic R-squared");
            Check(Math.Abs(f.ChiSquared-2)<1e-12 && Math.Abs(f.ReducedChiSquared-2.0/3)<1e-12,"analytic chi-squared independent of objective");
        }
        var flat=new Region { Left=0,Right=2 }; var flatC=Component.Create("POLY",0,2,1,0,2);
        flatC.P[0].Fixed=true;flat.Components.Add(flatC);
        Check(double.IsNaN(new Fitter(new Spectrum { X=new double[]{0,1,2},Y=new double[]{2,2,2} },flat).Run(10).RSquared),"constant R-squared undefined");
        var template=new Project { Data=s }; var region=new Region { Left=60,Right=145,Method=0 };
        region.Components.Add(Component.Create("POLY",90,10,1,60,145));
        region.Components.Add(Component.Create("G",94,180,1,60,145));
        region.Components.Add(Component.Create("G",106,105,1,60,145)); template.Regions.Add(region);
        File.Copy("outputs/demo-counts.txt","outputs/a,quoted.txt",true);
        File.WriteAllText("outputs/bad.txt","bad data");
        File.WriteAllText("outputs/short.txt","1\n2\n3");
        BatchExport.Run(template,new[]{"outputs/a,quoted.txt","outputs/bad.txt","outputs/short.txt","outputs/demo-counts.txt"},"outputs/folder-results.csv",300);
        var lines=File.ReadAllLines("outputs/folder-results.csv");
        Check(lines.Length==5 && lines.Count(l=>l.Contains("\"error\""))==2,"batch continues after errors");
        Check(lines[1].StartsWith("\"a,quoted.txt\"") && lines[1].Substring(lines[1].IndexOf(',')+1).Length>0,"CSV quoted filename");
        Check(region.Components[1].P[1].Value==94,"batch preserves template");
        string before=File.ReadAllText("outputs/demo-counts.txt");bool refused=false;
        try { BatchExport.Run(template,new[]{"outputs/demo-counts.txt"},"outputs/demo-counts.txt",300); }
        catch(ArgumentException) { refused=true; }
        Check(refused && before==File.ReadAllText("outputs/demo-counts.txt"),"never overwrite an input spectrum");
        var second=new Region { Left=60,Right=145,Method=1 };
        second.Components.Add(Component.Create("POLY",90,10,1,60,145));
        second.Components.Add(Component.Create("G",94,180,1,60,145));
        second.Components.Add(Component.Create("G",106,105,1,60,145));
        template.Regions.Add(second);
        BatchExport.Run(template,new[]{"outputs/demo-counts.txt"},"outputs/multiple-rois.csv",300);
        Check(File.ReadAllLines("outputs/multiple-rois.csv").Length==3,"one final row per file and ROI");
        var largeX=Enumerable.Range(2300,901).Select(i=>(double)i).ToArray();
        var largeData=new Spectrum { X=largeX,Y=largeX.Select(v=>500+9000*(3200-v)/900+
            38000*Math.Exp(-0.5*Math.Pow((v-2550)/60,2))+35000*Math.Exp(-0.5*Math.Pow((v-2900)/75,2))).ToArray() };
        foreach(int method in new[]{0,1,2}) {
            var lr=new Region { Left=2300,Right=3200,Method=method };
            var bg=Component.Create("POLY",2300,10000,1,2300,3200);bg.P[1].Fixed=false;lr.Components.Add(bg);
            foreach(double center in new[]{2530.0,2920.0})lr.Components.Add(Component.Create("G",center,20000,1,2300,3200));
            var lf=new Fitter(largeData,lr).Run(1000);
            Console.WriteLine("Large-scale method {0}: R2={1}, widths={2}, {3}",method,lf.RSquared,lr.Components[1].P[2].Value,lr.Components[2].P[2].Value);
            Check(lf.Converged && lf.Covariance && lf.RSquared>0.999999 && Math.Abs(lr.Components[1].P[2].Value-60)<0.01 && Math.Abs(lr.Components[2].P[2].Value-75)<0.01,"large-scale peaks recover");
            lr.Components[1].P[2].Value=0.01;bool rejected=false;
            try { new Fitter(largeData,lr).Run(10); }catch(ArgumentException){rejected=true;}
            Check(rejected,"undersampled free peak must not report convergence");
        }
        Console.WriteLine("OK: input, all three fits, project round trip, all 28 model shapes, analytic statistics, folder aggregation and errors.");
    }
}

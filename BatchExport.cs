using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Xml.Serialization;
namespace PhoFit {
public static class BatchExport {
    static string N(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("G17",CultureInfo.InvariantCulture); }
    static string Q(string v) { return "\""+(v??"").Replace("\"","\"\"")+"\""; }
    static string Prefix(Component c,int index) { return Component.FullName(c.Kind)+"_"+(index+1)+"_"; }
    static string[] Columns(Project template) {
        var columns=new List<string> { "file","roi","r_squared","chi_squared","reduced_chi_squared","cost","converged","status","error" };
        foreach(var r in template.Regions) for(int k=0;k<r.Components.Count;k++) {
            var c=r.Components[k];string prefix=Prefix(c,k);
            columns.Add(prefix+"area");columns.Add(prefix+"area_std_uncertainty");
            foreach(var p in c.P) { columns.Add(prefix+p.Name);columns.Add(prefix+p.Name+"_std_uncertainty"); }
        }
        return columns.Distinct().ToArray();
    }
    static void Write(TextWriter w,string[] columns,Dictionary<string,string> row) {
        w.WriteLine(string.Join(",",columns.Select(c=>Q(row.ContainsKey(c)?row[c]:""))));
    }
    static Project Copy(Project p) {
        var serializer=new XmlSerializer(typeof(Project));
        using(var stream=new MemoryStream()) {
            serializer.Serialize(stream,p); stream.Position=0;
            return (Project)serializer.Deserialize(stream);
        }
    }
    static Dictionary<string,string> Base(string file,int roi) {
        var row=new Dictionary<string,string> { {"file",Path.GetFileName(file)} };
        if(roi>0)row["roi"]=roi.ToString();
        return row;
    }
    public static string Run(Project template,string[] files,string output,int iterations) {
        if(files.Length==0)throw new ArgumentException("Chưa chọn tệp phổ.");
        if(!template.Regions.Any(r=>r.Components.Count>0))throw new ArgumentException("Cần ít nhất một ROI có mô hình.");
        if(files.Any(f=>string.Equals(Path.GetFullPath(f),Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Tệp CSV đầu ra phải khác tất cả tệp phổ đầu vào.");
        int success=0, failed=0, unconverged=0;var columns=Columns(template);
        using(var w=new StreamWriter(output,false,new UTF8Encoding(true))) {
            w.WriteLine(string.Join(",",columns));
            foreach(var file in files) {
                Project p;
                try { p=Copy(template); p.Data=Spectrum.Read(file); }
                catch(Exception e) { Error(w,columns,file,0,e); failed++; continue; }
                for(int j=0;j<p.Regions.Count;j++) {
                    var r=p.Regions[j]; if(r.Components.Count==0)continue;
                    try {
                        if(r.Left<p.Data.X.First() || r.Right>p.Data.X.Last())
                            throw new ArgumentException("Trục phổ không bao phủ toàn bộ ROI.");
                        var f=new Fitter(p.Data,r).Run(iterations);
                        var row=Base(file,j+1);
                        row["r_squared"]=N(f.RSquared);row["chi_squared"]=N(f.ChiSquared);row["reduced_chi_squared"]=N(f.ReducedChiSquared);
                        row["cost"]=N(f.Cost);row["converged"]=f.Converged.ToString();row["status"]=f.Converged?"ok":"not_converged";row["error"]=f.Warning;
                        for(int k=0;k<r.Components.Count;k++) {
                            var c=r.Components[k];string prefix=Prefix(c,k);
                            row[prefix+"area"]=N(f.Areas[k]);row[prefix+"area_std_uncertainty"]=N(f.AreaErrors[k]);
                            foreach(var parameter in c.P) {
                                row[prefix+parameter.Name]=N(parameter.Value);
                                row[prefix+parameter.Name+"_std_uncertainty"]=N(f.Errors[parameter]);
                            }
                        }
                        Write(w,columns,row);
                        success++; if(!f.Converged)unconverged++;
                    } catch(Exception e) { Error(w,columns,file,j+1,e); failed++; }
                }
                w.Flush();
            }
        }
        return "Đã xét "+files.Length+" tệp; khớp được "+success+" ROI, "+unconverged+" ROI chưa hội tụ, "+failed+" lỗi.\nToàn bộ kết quả được lưu tại:\n"+output;
    }
    static void Error(TextWriter w,string[] columns,string file,int roi,Exception e) {
        var row=Base(file,roi);row["status"]="error";row["error"]=e.Message;Write(w,columns,row);
    }
}
}

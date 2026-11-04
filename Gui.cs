using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace PhoFit {
class Plot : Panel {
    public Spectrum Data;
    public Region Roi;
    public FitResult Result;
    public bool Log, ResidualOnly, RegionView;
    public Action<double,double> Selected;
    public Action<double> CursorMoved;
    double left, right;
    int down;
    bool dragging;
    public Plot() { DoubleBuffered=true; BackColor=Color.White; }
    RectangleF Frame { get { return new RectangleF(74,16,Math.Max(1,Width-98),Math.Max(1,Height-58)); } }
    double Axis(int px) { return left + (right-left)*Math.Max(0,Math.Min(1,(px-Frame.Left)/Frame.Width)); }
    public void ZoomTo(double a,double b) { if(b>a){left=a;right=b;Invalidate();} }
    public void ResetView() {
        if (Data!=null) { left=Data.X.First(); right=Data.X.Last(); }
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        if (Data==null) return;
        if (e.Button==MouseButtons.Right) { if(Roi!=null && RegionView)ZoomTo(Roi.Left,Roi.Right);else ResetView(); return; }
        if (e.Button==MouseButtons.Left) { down=e.X; dragging=true; Capture=true; }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (Data!=null && CursorMoved!=null) CursorMoved(Axis(e.X));
    }
    protected override void OnMouseUp(MouseEventArgs e) {
        if (!dragging) return;
        dragging=false; Capture=false;
        if (Selected!=null) Selected(Axis(Math.Min(down,e.X)),Axis(Math.Max(down,e.X)));
    }
    protected override void OnMouseWheel(MouseEventArgs e) {
        if (Data==null) return;
        double x=Axis(e.X), factor=e.Delta>0 ? 0.8 : 1.25;
        double lo=Math.Max(Data.X.First(),x+(left-x)*factor);
        double hi=Math.Min(Data.X.Last(),x+(right-x)*factor);
        if (hi>lo && hi-lo>(Data.X.Last()-Data.X.First())*1e-7) { left=lo; right=hi; Invalidate(); }
    }
    double Transform(double v) { return Log ? Math.Sign(v)*Math.Log10(1+Math.Abs(v)) : v; }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if (ResidualOnly) { PaintResidual(e.Graphics); return; }
        if (Data==null) { e.Graphics.DrawString("Mở tệp phổ để bắt đầu.",Font,Brushes.DimGray,20,20); return; }
        var f=Frame; var g=e.Graphics;
        var rows=Enumerable.Range(0,Data.X.Length).Where(i=>Data.X[i]>=left && Data.X[i]<=right).ToArray();
        if (rows.Length==0 || right<=left) return;
        double min=Math.Min(0,Transform(rows.Min(i=>Data.Y[i]))), max=Math.Max(0,Transform(rows.Max(i=>Data.Y[i])));
        if (Result!=null) { max=Math.Max(max,Transform(Result.Model.Max())); min=Math.Min(min,Transform(Result.Model.Min())); }
        if (max<=min) max=min+1;
        Func<double,float> px=x=>f.Left+(float)((x-left)/(right-left))*f.Width;
        float bottom=f.Bottom;
        Func<double,float> py=y=>bottom-(float)((Transform(y)-min)/(max-min))*(bottom-f.Top);
        for (int j=0;j<=5;j++) {
            float x=f.Left+f.Width*j/5, y=f.Top+(bottom-f.Top)*j/5;
            g.DrawLine(Pens.Gainsboro,f.Left,y,f.Right,y);
            g.DrawString((max-(max-min)*j/5).ToString("G4"),Font,Brushes.DimGray,2,y-7);
            g.DrawString((left+(right-left)*j/5).ToString("G5"),Font,Brushes.DimGray,x-15,f.Bottom+4);
        }
        g.SetClip(f);
        if (Roi!=null) {
            float l=px(Math.Max(left,Roi.Left)), r=px(Math.Min(right,Roi.Right));
            if (r>l) using(var brush=new SolidBrush(Color.FromArgb(30,0,140,180))) g.FillRectangle(brush,l,f.Top,r-l,bottom-f.Top);
        }
        Draw(g,Pens.Black,rows.Select(i=>new PointF(px(Data.X[i]),py(Data.Y[i]))));
        if (Result!=null && Roi!=null) {
            int first=Array.FindIndex(Data.X,x=>x>=Roi.Left);
            Draw(g,Pens.Blue,rows.Select(i=>i-first).Where(j=>j>=0 && j<Result.Model.Length)
                .Select(j=>new PointF(px(Data.X[j+first]),py(Result.Model[j]))));
            Color[] colors={Color.SeaGreen,Color.DarkViolet,Color.Teal,Color.Sienna,Color.DeepPink};
            for(int k=0;k<Roi.Components.Count;k++) {
                var c=Roi.Components[k];
                using(var pen=new Pen(colors[k%colors.Length]) { DashStyle=System.Drawing.Drawing2D.DashStyle.Dot })
                    Draw(g,pen,rows.Where(i=>Data.X[i]>=Roi.Left && Data.X[i]<=Roi.Right)
                        .Select(i=>new PointF(px(Data.X[i]),py(c.Eval(Data.X[i],Roi.Left,Roi.Right-Roi.Left)))));
            }
        }
        g.ResetClip();
        g.DrawRectangle(Pens.Gray,f.Left,f.Top,f.Width,bottom-f.Top);
        g.DrawString(Log ? "log10(1+|y|), có dấu" : "Số đếm",Font,Brushes.Black,f.Left,0);

        g.DrawString("Kênh / năng lượng",Font,Brushes.Black,f.Left,f.Bottom+23);
    }
    void PaintResidual(Graphics g) {
        if(Result==null || Roi==null || Data==null) {
            g.DrawString("Khớp ROI để xem phần dư.",Font,Brushes.DimGray,20,18); return;
        }
        var f=Frame;
        float mid=f.Top+f.Height/2;
        double max=Math.Max(1e-10,Result.Residual.Max(v=>Math.Abs(v)));
        int first=Array.FindIndex(Data.X,x=>x>=Roi.Left);
        g.DrawRectangle(Pens.Gray,f.Left,f.Top,f.Width,f.Height);
        g.DrawLine(Pens.Firebrick,f.Left,mid,f.Right,mid);
        Draw(g,Pens.Black,Result.Residual.Select((v,i)=>new PointF(
            f.Left+(float)((Data.X[first+i]-Roi.Left)/(Roi.Right-Roi.Left))*f.Width,
            mid-(float)(v/max)*f.Height*0.45f)));
        g.DrawString(max.ToString("G4"),Font,Brushes.Black,5,f.Top);
        g.DrawString("0",Font,Brushes.Black,5,mid-7);
        g.DrawString((-max).ToString("G4"),Font,Brushes.Black,5,f.Bottom-13);
        g.DrawString(Roi.Left.ToString("G6"),Font,Brushes.Black,f.Left,f.Bottom+4);
        g.DrawString(Roi.Right.ToString("G6"),Font,Brushes.Black,f.Right-35,f.Bottom+4);
    }
    static void Draw(Graphics g, Pen pen, IEnumerable<PointF> points) {
        var a=points.Where(p=>!float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&!float.IsInfinity(p.X)&&!float.IsInfinity(p.Y)).ToArray();
        if(a.Length>1) g.DrawLines(pen,a);
        else if(a.Length==1) g.DrawEllipse(pen,a[0].X-1,a[0].Y-1,2,2);
    }
}

class MainForm : Form {
    Project project=new Project();
    readonly Dictionary<Region,FitResult> results=new Dictionary<Region,FitResult>();
    readonly Plot plot=new Plot { Dock=DockStyle.Fill,RegionView=true };
    readonly Plot overview=new Plot { Dock=DockStyle.Fill };
    readonly Plot residual=new Plot { Dock=DockStyle.Fill,ResidualOnly=true };
    Form spectrumWindow, roiWindow, residualWindow, propertiesWindow;

    readonly ComboBox regions=Combo(), types=Combo(), methods=Combo();
    readonly TextBox left=new TextBox { Width=90 }, right=new TextBox { Width=90 };
    readonly NumericUpDown iterations=new NumericUpDown { Minimum=1,Maximum=5000,Value=200,Width=70 };
    readonly ListBox components=new ListBox { Dock=DockStyle.Top,Height=115 };
    readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill };
    readonly TextBox report=new TextBox { Multiline=true,ReadOnly=true,Dock=DockStyle.Bottom,Height=130,ScrollBars=ScrollBars.Vertical };
    readonly ToolStripStatusLabel status=new ToolStripStatusLabel();
    readonly MenuStrip menu=new MenuStrip();
    double cursor=double.NaN;
    bool updating;
    Region Current { get { return regions.SelectedIndex<0 ? null : project.Regions[regions.SelectedIndex]; } }
    Component CurrentComponent { get { return Current==null || components.SelectedIndex<0 ? null : Current.Components[components.SelectedIndex]; } }
    static ComboBox Combo() { return new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=120 }; }
    static string N(double v) { return v.ToString("G17",CultureInfo.InvariantCulture); }
    static string Short(double v) { return double.IsNaN(v) ? "—" : v.ToString("G6",CultureInfo.InvariantCulture); }
    public MainForm() {
        Text="PhổFit · Khớp phổ đa thành phần"; Width=1360; Height=830; MinimumSize=new Size(990,620);
        Font=new Font("Segoe UI",9);
        var file=new ToolStripMenuItem("Tệp");
        Item(file,"Mở phổ…",()=>OpenSpectrum()); Item(file,"Mở dự án…",()=>LoadProject());
        Item(file,"Lưu dự án…",()=>SaveProject()); Item(file,"Xuất CSV vùng hiện tại…",()=>ExportCurrent());
        Item(file,"Khớp toàn bộ thư mục…",()=>BatchFolder()); Item(file,"Khớp hàng loạt…",()=>Batch()); Item(file,"Cộng phổ…",()=>Sum());
        IsMdiContainer=true;
        var view=new ToolStripMenuItem("Xem");
        Item(view,"Thuộc tính đỉnh",()=>FocusWindow(propertiesWindow));
        Item(view,"Hiệu chuẩn trục X…",Calibrate);
        var display=new ToolStripMenuItem("Hiển thị");
        Item(display,"Toàn phổ",()=>{overview.ResetView();FocusWindow(spectrumWindow);});
        Item(display,"Vừa vùng ROI",()=>{if(Current!=null)plot.ZoomTo(Current.Left,Current.Right);FocusWindow(roiWindow);});
        Item(display,"Đổi thang tuyến tính / log",()=>{plot.Log=overview.Log=!plot.Log;plot.Invalidate();overview.Invalidate();});
        Item(display,"Xuất phổ trừ nền đã khớp…",Subtract);
        Item(display,"Xuất phổ trừ nền tích phân…",IntegralBackground);
        var windows=new ToolStripMenuItem("Cửa sổ");
        Item(windows,"Bố cục phân tích",Arrange);
        Item(windows,"Xếp tầng",()=>LayoutMdi(MdiLayout.Cascade));
        menu.MdiWindowListItem=windows;
        var help=new ToolStripMenuItem("Trợ giúp");
        Item(help,"Giới thiệu…",About);
        Item(help,"Thao tác",()=>MessageBox.Show(this,"Kéo trên Phổ toàn phần để đặt ROI. Nhấp trong ROI để chọn tâm khởi tạo. Chọn hàm ở Thuộc tính đỉnh, sau đó bấm LS, χ² hoặc Poisson.","PhổFit"));
        menu.Items.AddRange(new ToolStripItem[]{file,view,display,windows,help}); MainMenuStrip=menu;
        var bar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=36,Padding=new Padding(3),WrapContents=false,AutoScroll=true,BackColor=SystemColors.Control };
        types.Items.AddRange(Component.Kinds.Select(k=>Component.FullName(k)).ToArray()); types.SelectedIndex=0;
        types.Width=300;types.DropDownWidth=650;components.HorizontalScrollbar=true;
        methods.Items.AddRange(new object[]{"Bình phương tối thiểu","Chi bình phương Neyman","Poisson (deviance)"}); methods.Width=190;
        bar.Controls.AddRange(new Control[]{Button("Mở",OpenSpectrum),Button("Toàn phổ",()=>overview.ResetView()),
            Button("Chọn ROI",()=>FocusWindow(spectrumWindow)),regions,Button("+ ROI",NewRegion),Button("Xóa ROI",DeleteRegion),
            Label("Từ"),left,Label("đến"),right,Button("Áp dụng",ApplyBounds),
            Button("LS",()=>FitBy(0)),Button("χ²",()=>FitBy(1)),Button("Poisson",()=>FitBy(2)),
            Button("Khớp mọi ROI",FitAll),Button("Thuộc tính",()=>FocusWindow(propertiesWindow))});
        var componentBar=new FlowLayoutPanel { Dock=DockStyle.Top,Height=70 };
        componentBar.Controls.AddRange(new Control[]{types,Button("Thêm hàm",AddComponent),Button("Xóa hàm",DeleteComponent),Button("Ước lượng độ rộng",EstimateWidths),methods,Label("Vòng"),iterations});
        var properties=new Panel { Dock=DockStyle.Fill };
        properties.Controls.Add(grid); properties.Controls.Add(report);
        properties.Controls.Add(components); properties.Controls.Add(componentBar);
        grid.Columns.Add("name","Tham số"); grid.Columns.Add("value","Giá trị"); grid.Columns.Add("min","Min"); grid.Columns.Add("max","Max");
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name="fixed",HeaderText="Cố định" });
        grid.Columns.Add("link","Nhóm"); grid.Columns.Add("err","u(1σ)");
        grid.Columns[0].ReadOnly=true; grid.Columns[6].ReadOnly=true;
        grid.CellValidating+=ValidateCell; grid.CellEndEdit+=EditCell;
        grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell) grid.EndEdit();};
        regions.SelectedIndexChanged+=(s,e)=>{if(!updating) ShowRegion();};
        components.SelectedIndexChanged+=(s,e)=>{if(!updating) ShowParameters();};
        methods.SelectedIndexChanged+=(s,e)=>{if(!updating && Current!=null){Current.Method=methods.SelectedIndex; InvalidateFit();}};
        plot.CursorMoved=x=>status.Text="ROI · X = "+Short(x)+"     |     Nhấp để đặt tâm đỉnh · Cuộn để thu phóng";
        overview.CursorMoved=x=>status.Text="Phổ · X = "+Short(x)+"     |     Kéo để chọn ROI · Cuộn để thu phóng";
        plot.Selected=(a,b)=>Safe(()=>{if(a==b){cursor=a;status.Text="Tâm khởi tạo X = "+Short(a);}else plot.ZoomTo(a,b);});
        overview.Selected=(a,b)=>Safe(()=>{if(a==b){cursor=a;}else if(Current!=null){
            left.Text=N(a);right.Text=N(b);ApplyBounds();FocusWindow(roiWindow);
        }});
        var footer=new StatusStrip(); footer.Items.Add(status);
        Controls.Add(bar); Controls.Add(menu); Controls.Add(footer);
        spectrumWindow=Child("Phổ toàn phần",overview);
        roiWindow=Child("Vùng quan tâm (ROI)",plot);
        residualWindow=Child("Phần dư",residual);
        propertiesWindow=Child("Thuộc tính đỉnh",properties);
        Shown+=(s,e)=>{foreach(Form f in MdiChildren)f.Show();Arrange();};
        ResizeEnd+=(s,e)=>Arrange();
    }
    void About() {
        using(var dialog=new Form { Text="Giới thiệu PhổFit",ClientSize=new Size(550,300),
            FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,
            MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,Font=Font }) {
            var title=new Label { Text="PhổFit 1.2.4",AutoSize=true,Location=new Point(24,20),Font=new Font(Font.FontFamily,14,FontStyle.Bold) };
            var details=new Label { AutoSize=true,Location=new Point(24,60),Text=
                "Lê Hoàng Minh\nBộ môn Vật lý hạt nhân\nKhoa Vật lý-Vật lý Kỹ thuật\nTrường Đại học khoa học tự nhiên\nĐại học Quốc gia Thành phố Hồ Chí Minh" };
            var email=new LinkLabel { AutoSize=true,Location=new Point(24,190),Text="email: hoangminh@hcmus.edu.vn" };
            email.Links.Clear();email.Links.Add(7,"hoangminh@hcmus.edu.vn".Length);
            email.LinkClicked+=(sender,e)=>Safe(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:hoangminh@hcmus.edu.vn") { UseShellExecute=true }));
            var close=new Button { Text="Đóng",DialogResult=DialogResult.OK,Location=new Point(440,250),Size=new Size(85,30) };
            dialog.Controls.AddRange(new Control[]{title,details,email,close});
            dialog.AcceptButton=close;dialog.CancelButton=close;dialog.ShowDialog(this);
        }
    }
    Form Child(string title,Control content) {
        var f=new Form { Text=title,MdiParent=this,StartPosition=FormStartPosition.Manual,ShowIcon=false,MinimumSize=new Size(250,140) };
        f.Controls.Add(content);
        f.FormClosing+=(s,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;f.Hide();}};
        return f;
    }
    void FocusWindow(Form f) { f.Show();if(f.WindowState==FormWindowState.Minimized)f.WindowState=FormWindowState.Normal;f.Activate(); }
    void Arrange() {
        var area=Controls.OfType<MdiClient>().First().ClientSize;
        if(area.Width<750 || area.Height<480)return;
        int propertyWidth=Math.Max(415,area.Width*34/100);
        int w=area.Width-propertyWidth-12;
        int top=area.Height*34/100, mid=area.Height*39/100;
        foreach(Form f in MdiChildren){f.Show();f.WindowState=FormWindowState.Normal;}
        spectrumWindow.SetBounds(4,4,w,top-5);
        roiWindow.SetBounds(4,top+3,w,mid-5);
        residualWindow.SetBounds(4,top+mid+2,w,area.Height-top-mid-7);
        propertiesWindow.SetBounds(w+8,4,propertyWidth,area.Height-8);
    }
    void FitBy(int method) {
        if(Current==null)return;
        methods.SelectedIndex=method;
        FitCurrent();
    }
    static Label Label(string s) { return new Label { Text=s,AutoSize=true,Margin=new Padding(5,7,2,0) }; }
    Button Button(string title,Action action) {
        var b=new Button { Text=title,AutoSize=true }; b.Click+=(s,e)=>Safe(action); return b;
    }
    void Item(ToolStripMenuItem menuItem,string title,Action action) { menuItem.DropDownItems.Add(title,null,(s,e)=>Safe(action)); }
    void Safe(Action action) {
        try { action(); }
        catch(Exception e) { MessageBox.Show(this,e.Message,"PhổFit",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    string[] Open(bool multi,string filter) {
        using(var d=new OpenFileDialog { Multiselect=multi,Filter=filter })
            return d.ShowDialog(this)==DialogResult.OK ? d.FileNames : new string[0];
    }
    string Save(string filter) {
        using(var d=new SaveFileDialog { Filter=filter })
            return d.ShowDialog(this)==DialogResult.OK ? d.FileName : null;
    }
    const string Spectra="Phổ văn bản|*.txt;*.csv;*.asc;*.tsv;*.dat|Tất cả|*.*";
    const string Projects="Dự án PhổFit|*.phofit";
    const string Csv="CSV|*.csv";
    void OpenSpectrum() {
        var files=Open(false,Spectra); if(files.Length==0)return;
        Install(new Project { Data=Spectrum.Read(files[0]) });
    }
    void Install(Project p) {
        project=p; results.Clear(); cursor=double.NaN; plot.Data=overview.Data=residual.Data=p.Data; plot.ResetView(); overview.ResetView();
        if(p.Regions.Count==0) NewRegion(); else RefreshRegions(0);
    }
    void RequireSpectrum() { if(project.Data==null) throw new InvalidOperationException("Hãy mở phổ trước."); }
    void NewRegion() {
        RequireSpectrum();
        project.Regions.Add(new Region { Left=project.Data.X.First(),Right=project.Data.X.Last() });
        RefreshRegions(project.Regions.Count-1);
    }
    void DeleteRegion() {
        if(Current==null)return;
        int index=regions.SelectedIndex;
        results.Remove(Current); project.Regions.RemoveAt(index);
        RefreshRegions(Math.Min(index,project.Regions.Count-1));
    }
    void RefreshRegions(int index) {
        updating=true; regions.Items.Clear();
        for(int i=0;i<project.Regions.Count;i++) regions.Items.Add("ROI "+(i+1));
        regions.SelectedIndex=index; updating=false; ShowRegion();
    }
    void ShowRegion() {
        updating=true;
        var r=Current;
        left.Text=r==null ? "" : N(r.Left); right.Text=r==null ? "" : N(r.Right);
        methods.SelectedIndex=r==null ? -1 : r.Method;
        updating=false; if(r!=null)plot.ZoomTo(r.Left,r.Right); RefreshComponents(0);
    }
    void ApplyBounds() {
        if(Current==null)return;
        double a=Spectrum.Number(left.Text),b=Spectrum.Number(right.Text);
        if(b<=a || a<project.Data.X.First() || b>project.Data.X.Last())
            throw new ArgumentException("Giới hạn ROI phải tăng và nằm trong trục phổ.");
        if(a==Current.Left && b==Current.Right)return;
        Current.Left=a; Current.Right=b; plot.ZoomTo(a,b); InvalidateFit();
    }
    void RefreshComponents(int index) {
        updating=true; components.Items.Clear();
        if(Current!=null) for(int i=0;i<Current.Components.Count;i++) components.Items.Add((i+1)+"  "+Component.FullName(Current.Components[i].Kind));
        components.SelectedIndex=Math.Min(index,components.Items.Count-1);
        updating=false; ShowParameters(); RefreshPlot();
    }
    void ShowParameters() {
        updating=true; grid.Rows.Clear();
        FitResult result; var c=CurrentComponent;
        if(c!=null) foreach(var p in c.P) {
            string uncertainty=results.TryGetValue(Current,out result) ? Short(result.Errors[p]) : "";
            grid.Rows.Add(p.Name,N(p.Value),double.IsNegativeInfinity(p.Min)?"":N(p.Min),
                double.IsPositiveInfinity(p.Max)?"":N(p.Max),p.Fixed,p.Link,uncertainty);
        }
        updating=false;
    }
    void ValidateCell(object sender,DataGridViewCellValidatingEventArgs e) {
        if(updating || CurrentComponent==null || e.RowIndex<0 || e.ColumnIndex<1 || e.ColumnIndex>3) return;
        string value=Convert.ToString(e.FormattedValue);
        try {
            var p=CurrentComponent.P[e.RowIndex];
            if(e.ColumnIndex==2 && value.Trim().Length==0) return;
            if(e.ColumnIndex==3 && value.Trim().Length==0) return;
            double n=Spectrum.Number(value);
            if((e.ColumnIndex==2 && n>p.Max)||(e.ColumnIndex==3 && n<p.Min))
                throw new ArgumentException("Min phải nhỏ hơn hoặc bằng Max.");
            bool positive=p.Name.StartsWith("sigma") || p.Name.StartsWith("tau") || p.Name=="endpoint_keV";
            bool nonnegative=p.Name=="gamma_HWHM";
            if ((positive || nonnegative) && e.ColumnIndex!=3 && (positive ? n<=0 : n<0))
                throw new ArgumentException("Tham số độ rộng hoặc điểm cuối nằm ngoài miền hợp lệ.");
        } catch(Exception err) { e.Cancel=true; status.Text="Giá trị không hợp lệ. "+err.Message; }
    }
    void EditCell(object sender,DataGridViewCellEventArgs e) {
        if(updating || CurrentComponent==null || e.RowIndex<0)return;
        var p=CurrentComponent.P[e.RowIndex]; var row=grid.Rows[e.RowIndex];
        string v=Convert.ToString(row.Cells[e.ColumnIndex].Value);
        Safe(()=>{
            switch(e.ColumnIndex) {
                case 1:p.Value=Spectrum.Number(v);break;
                case 2:p.Min=string.IsNullOrWhiteSpace(v)?double.NegativeInfinity:Spectrum.Number(v);break;
                case 3:p.Max=string.IsNullOrWhiteSpace(v)?double.PositiveInfinity:Spectrum.Number(v);break;
                case 4:p.Fixed=Convert.ToBoolean(row.Cells[4].Value);break;
                case 5:p.Link=v;break;
            }
            InvalidateFit();
        });
    }
    void AddComponent() {
        if(Current==null)return;
        ApplyBounds();
        var d=project.Data; var r=Current;
        int[] rows=Enumerable.Range(0,d.X.Length).Where(i=>d.X[i]>=r.Left && d.X[i]<=r.Right).ToArray();
        if(rows.Length<2)throw new ArgumentException("ROI cần ít nhất hai điểm.");
        double bg=Math.Max(0,(d.Y[rows.First()]+d.Y[rows.Last()])/2);
        double target=cursor>=r.Left && cursor<=r.Right ? cursor : d.X[rows.OrderByDescending(i=>d.Y[i]).First()];
        int j=rows.OrderBy(i=>Math.Abs(d.X[i]-target)).First();
        string kind=Component.Kinds[types.SelectedIndex];
        var c=Component.Create(kind,d.X[j],Math.Max(1,d.Y[j]-bg),
            (d.X[rows.Last()]-d.X[rows.First()])/(rows.Length-1),r.Left,r.Right);
        if(c.Background)c.P[kind=="STEP"||kind=="DSTEP"?1:0].Value=bg;
        r.Components.Add(c); results.Remove(r); RefreshComponents(r.Components.Count-1);
    }
    void EstimateWidths() {
        if(Current==null || !grid.EndEdit())return;
        ApplyBounds();
        var r=Current;var d=project.Data;
        var ids=Enumerable.Range(0,d.X.Length).Where(i=>d.X[i]>=r.Left && d.X[i]<=r.Right).ToArray();
        if(ids.Length<3)throw new ArgumentException("ROI cần ít nhất ba điểm.");
        var peaks=r.Components.Where(c=>c.Kind=="G").OrderBy(c=>c.P[1].Value).ToArray();
        for(int k=0;k<peaks.Length;k++) {
            var c=peaks[k];double center=c.P[1].Value;
            double lo=k==0?r.Left:(peaks[k-1].P[1].Value+center)/2;
            double hi=k==peaks.Length-1?r.Right:(center+peaks[k+1].P[1].Value)/2;
            var local=ids.Where(i=>d.X[i]>=lo && d.X[i]<=hi).ToArray();if(local.Length<3)continue;
            int top=local.OrderByDescending(i=>d.Y[i]).First();
            double baseline=Math.Min(d.Y[local.First()],d.Y[local.Last()]);
            double half=baseline+(d.Y[top]-baseline)/2;
            int a=top,b=top;
            while(a>local.First() && d.Y[a]>half)a--;
            while(b<local.Last() && d.Y[b]>half)b++;
            double width=(d.X[b]-d.X[a])/2.354820045;
            if(!c.P[2].Fixed)c.P[2].Value=Math.Max(c.P[2].Min,Math.Min(c.P[2].Max,Math.Max(width,(d.X[local.Last()]-d.X[local.First()])/20)));
            if(!c.P[0].Fixed)c.P[0].Value=Math.Max(c.P[0].Min,Math.Min(c.P[0].Max,Math.Max(1,d.Y[top]-baseline)));
            if(!c.P[1].Fixed)c.P[1].Value=Math.Max(c.P[1].Min,Math.Min(c.P[1].Max,d.X[top]));
        }
        InvalidateFit();ShowParameters();
        status.Text="Đã ước lượng tâm, biên độ và sigma cho các đỉnh Gaussian không cố định; bấm LS/χ²/Poisson để khớp.";
    }
    void DeleteComponent() {
        if(CurrentComponent==null)return;
        int i=components.SelectedIndex;
        Current.Components.RemoveAt(i); results.Remove(Current); RefreshComponents(i);
    }
    void InvalidateFit() {
        if(Current!=null)results.Remove(Current);
        foreach(DataGridViewRow row in grid.Rows)row.Cells[6].Value="";
        RefreshPlot();
    }
    void RefreshPlot() {
        FitResult fit=null;
        if(Current!=null)results.TryGetValue(Current,out fit);
        plot.Roi=overview.Roi=residual.Roi=Current; plot.Result=residual.Result=fit; plot.Invalidate(); overview.Invalidate(); residual.Invalidate();
        if(fit==null) { report.Text=""; return; }
        var lines=new List<string> {
            (fit.Converged?"Hội tụ":"Chưa hội tụ")+" | "+fit.Iterations+" vòng | Chi phí "+Short(fit.Cost)+" | Bậc tự do "+fit.Dof+
                " | Chi phí/bậc "+Short(fit.Cost/fit.Dof),
            "R-squared = "+Short(fit.RSquared)+" | χ² (Neyman) = "+Short(fit.ChiSquared)+" | χ²/bậc = "+Short(fit.ReducedChiSquared),
            fit.Covariance ? "Diện tích trong ROI (số đếm) ± u(1σ)" : "Không ước lượng được hiệp phương sai."
        };
        for(int i=0;i<Current.Components.Count;i++) lines.Add((i+1)+" "+Component.FullName(Current.Components[i].Kind)+"   "+Short(fit.Areas[i])+" ± "+Short(fit.AreaErrors[i]));
        if(!string.IsNullOrEmpty(fit.Warning))lines.Add(fit.Warning);
        report.Lines=lines.ToArray();
    }
    T Busy<T>(Func<T> work) {
        UseWaitCursor=true;
        try { return work(); } finally { UseWaitCursor=false; }
    }
    void FitCurrent() {
        if(Current==null)return;
        if(!grid.EndEdit())return;
        ApplyBounds(); FitRegion(Current); ShowParameters(); RefreshPlot();
    }
    void FitRegion(Region roi) {
        results.Remove(roi);
        try { results[roi]=Busy(()=>new Fitter(project.Data,roi).Run((int)iterations.Value)); }
        finally { RefreshPlot(); }
    }
    void FitAll() {
        if(!grid.EndEdit())return;
        ApplyBounds();
        foreach(var roi in project.Regions.Where(r=>r.Components.Count>0))FitRegion(roi);
        ShowParameters();RefreshPlot();
    }
    static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Project));
    static Project ReadProject(string file) { using(var s=File.OpenRead(file))return (Project)Serializer.Deserialize(s); }
    static Project CopyProject(Project p) {
        using(var s=new MemoryStream()){Serializer.Serialize(s,p);s.Position=0;return (Project)Serializer.Deserialize(s);}
    }
    void LoadProject() { var f=Open(false,Projects);if(f.Length>0)Install(ReadProject(f[0])); }
    void SaveProject() {
        RequireSpectrum();if(!grid.EndEdit())return;
        ApplyBounds(); var f=Save(Projects);
        if(f!=null) using(var s=File.Create(f))Serializer.Serialize(s,project);
    }
    static void WriteSummary(TextWriter w,int roiIndex,Region r,FitResult fit) {
        w.WriteLine("roi,component,function,parameter,value,std_uncertainty,converged,cost,dof,method,r_squared,chi_squared,reduced_chi_squared");
        for(int i=0;i<r.Components.Count;i++) {
            var c=r.Components[i];
            foreach(var p in c.P) SummaryRow(w,roiIndex,i,Component.FullName(c.Kind),p.Name,p.Value,fit.Errors[p],fit,r.Method);
            SummaryRow(w,roiIndex,i,Component.FullName(c.Kind),"ROI_area",fit.Areas[i],fit.AreaErrors[i],fit,r.Method);
        }
    }
    static void SummaryRow(TextWriter w,int roi,int i,string kind,string name,double value,double u,FitResult f,int method) {
        w.WriteLine(string.Join(",",new[]{(roi+1).ToString(),(i+1).ToString(),kind,name,N(value),N(u),
            f.Converged.ToString(),N(f.Cost),f.Dof.ToString(),method.ToString(),N(f.RSquared),N(f.ChiSquared),N(f.ReducedChiSquared)}));
    }
    void ExportCurrent() {
        FitResult fit;
        if(Current==null || !results.TryGetValue(Current,out fit))throw new InvalidOperationException("Hãy khớp ROI trước.");
        var f=Save(Csv); if(f==null)return;
        using(var w=File.CreateText(f))WriteSummary(w,regions.SelectedIndex,Current,fit);
        using(var w=File.CreateText(Path.ChangeExtension(f,"curve.csv"))) {
            w.WriteLine("x,counts,model,residual,"+string.Join(",",Current.Components.Select((c,i)=>Component.FullName(c.Kind)+"_"+(i+1))));
            int j=0;
            for(int i=0;i<project.Data.X.Length;i++) {
                double x=project.Data.X[i]; if(x<Current.Left || x>Current.Right)continue;
                var cells=new[]{N(x),N(project.Data.Y[i]),N(fit.Model[j]),N(fit.Residual[j])};
                w.WriteLine(string.Join(",",cells.Concat(Current.Components.Select(c=>N(c.Eval(x,Current.Left,Current.Right-Current.Left))))));
                j++;
            }
        }
    }
    async void BatchFolder() {
        try {
            if(!grid.EndEdit())return;
            ApplyBounds();
            if(!project.Regions.Any(r=>r.Components.Count>0))throw new InvalidOperationException("Cần ít nhất một ROI có mô hình.");
            string folder;
            using(var d=new FolderBrowserDialog { Description="Chọn thư mục chứa phổ (không gồm thư mục con)" }) {
                if(d.ShowDialog(this)!=DialogResult.OK)return;
                folder=d.SelectedPath;
            }
            var output=Save(Csv); if(output==null)return;
            var extensions=new[]{".txt",".csv",".asc",".tsv",".dat"};
            var files=Directory.GetFiles(folder).Where(f=>extensions.Contains(Path.GetExtension(f).ToLowerInvariant()) &&
                !string.Equals(Path.GetFullPath(f),Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f).ToArray();
            if(files.Length==0)throw new InvalidOperationException("Không tìm thấy tệp phổ trong thư mục.");
            var template=CopyProject(project); int count=(int)iterations.Value;
            Enabled=false; UseWaitCursor=true;
            try {
                var summary=await System.Threading.Tasks.Task.Run(()=>BatchExport.Run(template,files,output,count));
                MessageBox.Show(this,summary,"Khớp toàn bộ thư mục");
            } finally { Enabled=true; UseWaitCursor=false; }
        } catch(Exception e) { MessageBox.Show(this,e.Message,"PhổFit",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    async void Batch() {
        try {
            if(!grid.EndEdit())return;
            ApplyBounds();
            if(!project.Regions.Any(r=>r.Components.Count>0))throw new InvalidOperationException("Cần ít nhất một ROI có mô hình.");
            var files=Open(true,Spectra);if(files.Length==0)return;
            var output=Save(Csv);if(output==null)return;
            var template=CopyProject(project);int count=(int)iterations.Value;
            Enabled=false;UseWaitCursor=true;
            try {
                var summary=await System.Threading.Tasks.Task.Run(()=>BatchExport.Run(template,files,output,count));
                MessageBox.Show(this,summary,"Khớp hàng loạt — CSV tổng hợp");
            } finally { Enabled=true;UseWaitCursor=false; }
        } catch(Exception e) { MessageBox.Show(this,e.Message,"PhổFit",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    void Sum() {
        var files=Open(true,Spectra);if(files.Length==0)return;
        var sum=Spectrum.Read(files[0]);
        foreach(var f in files.Skip(1)) {
            var s=Spectrum.Read(f);
            if(!sum.X.SequenceEqual(s.X))throw new ArgumentException("Các phổ phải có cùng trục X.");
            for(int i=0;i<sum.Y.Length;i++)sum.Y[i]+=s.Y[i];
        }
        Install(new Project { Data=sum });
    }
    void Subtract() {
        if(Current==null || !results.ContainsKey(Current) || !Current.Components.Any(c=>c.Background))
            throw new InvalidOperationException("Hãy khớp ROI có thành phần nền.");
        var f=Save(Csv); if(f==null)return;
        using(var w=File.CreateText(f)) {
            w.WriteLine("x,counts,background,net_counts");
            for(int i=0;i<project.Data.X.Length;i++) {
                double x=project.Data.X[i]; if(x<Current.Left || x>Current.Right)continue;
                double b=Current.Components.Where(c=>c.Background).Sum(c=>c.Eval(x,Current.Left,Current.Right-Current.Left));
                w.WriteLine(string.Join(",",new[]{N(x),N(project.Data.Y[i]),N(b),N(project.Data.Y[i]-b)}));
            }
        }
    }
    void IntegralBackground() {
        if(Current==null)return;
        ApplyBounds();
        var data=project.Data;
        var ids=Enumerable.Range(0,data.X.Length).Where(i=>data.X[i]>=Current.Left && data.X[i]<=Current.Right).ToArray();
        if(ids.Length<3)throw new ArgumentException("Nền tích phân cần ít nhất ba điểm.");
        var y=ids.Select(i=>data.Y[i]).ToArray();
        var b=new double[y.Length];
        for(int iteration=0;iteration<2;iteration++) {
            var integral=new double[y.Length];
            for(int i=1;i<y.Length;i++)integral[i]=integral[i-1]+(y[i-1]-b[i-1]+y[i]-b[i])/2;
            double total=integral.Last();
            if(Math.Abs(total)<=1e-12*Math.Max(1,y.Sum(v=>Math.Abs(v))))
                throw new ArgumentException("Tổng tích phân gần bằng 0. Hãy chọn lại ROI.");
            for(int i=0;i<b.Length;i++)b[i]=y[0]+(y.Last()-y[0])*integral[i]/total;
        }
        var f=Save(Csv);if(f==null)return;
        using(var w=File.CreateText(f)) {
            w.WriteLine("x,counts,background,net_counts");
            for(int i=0;i<ids.Length;i++)w.WriteLine(string.Join(",",new[]{N(data.X[ids[i]]),N(y[i]),N(b[i]),N(y[i]-b[i])}));
        }
    }
    void Calibrate() {
        RequireSpectrum();
        using(var dialog=new Form { Text="Hiệu chuẩn X",Width=420,Height=170,StartPosition=FormStartPosition.CenterParent }) {
            var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(10) };
            var t=new TextBox { Text="0, 1, 0",Width=360 };
            var ok=new Button { Text="Áp dụng",DialogResult=DialogResult.OK };
            layout.Controls.AddRange(new Control[]{Label("X mới = a + b·X + c·X². Nhập a, b, c."),t,ok});
            dialog.Controls.Add(layout);dialog.AcceptButton=ok;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            var coefficients=t.Text.Split(new char[]{','}).Select(Spectrum.Number).ToArray();
            if(coefficients.Length!=3)throw new FormatException("Cần đúng ba hệ số.");
            var x=project.Data.X.Select(v=>coefficients[0]+v*(coefficients[1]+v*coefficients[2])).ToArray();
            if(x.Any(v=>double.IsNaN(v)||double.IsInfinity(v)) || x.Skip(1).Where((v,i)=>v<=x[i]).Any())
                throw new ArgumentException("Trục hiệu chuẩn phải hữu hạn và tăng.");
            if(project.Regions.Any(r=>r.Components.Count>0) && MessageBox.Show(this,
                "Hiệu chuẩn tạo ROI mới vì tham số hàm phụ thuộc trục X. Tiếp tục?",
                "Hiệu chuẩn",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            Install(new Project { Data=new Spectrum { X=x,Y=project.Data.Y } });
        }
    }
}
static class Program {
    [STAThread] static void Main() {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(sender,e)=>ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(sender,e)=>ReportError(e.ExceptionObject as Exception);
        try {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        } catch(Exception e) { ReportError(e); }
    }
    static void ReportError(Exception error) {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhoFit","startup-error.log");
        string detail=error==null ? "Unknown error" : error.ToString();
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path,DateTime.Now.ToString("s")+Environment.NewLine+detail+Environment.NewLine);
        } catch { path="(Không ghi được nhật ký)"; }
        MessageBox.Show("Không thể chạy PhổFit.\n"+detail+"\n\nNhật ký: "+path,"PhổFit — lỗi",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
}
}




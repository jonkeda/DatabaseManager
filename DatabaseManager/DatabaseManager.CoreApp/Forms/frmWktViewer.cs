//using DatabaseInterpreter.Geometry;

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.SqlServer.Types;

namespace DatabaseManager;

public partial class frmWktViewer : Form
{
    private readonly float defaultLimitMaxScale = 30;

    private GeometryInfo geomInfo;
    private bool isSettingZoomBar;
    private readonly Color linePenColor = Color.Red;
    private readonly Color polygonPenColor = Color.Green;
    private float? scale;

    public frmWktViewer()
    {
        InitializeComponent();
    }

    public frmWktViewer(bool isGeography, string content)
    {
        InitializeComponent();

        if (isGeography) rbGeography.Checked = true;

        if (!string.IsNullOrEmpty(content))
        {
            txtContent.Text = content.Trim();

            ShowGeometry(content);
        }
    }

    private Pen linePen => new(new SolidBrush(linePenColor), 0);
    private Pen polygonPen => new(new SolidBrush(polygonPenColor), 0);

    private void frmGeometryViewer_Load(object sender, EventArgs e)
    {
        DoubleBuffered = true;
    }

    private void btnView_Click(object sender, EventArgs e)
    {
        var content = txtContent.Text.Trim();

        if (string.IsNullOrEmpty(content))
        {
            MessageBox.Show("Please enter content.");
            return;
        }

        ResetValues();

        ShowGeometry(content);
    }

    private void ResetValues()
    {
        ResetScale();
        geomInfo = null;
    }

    private void ResetScale()
    {
        scale = default;
        isSettingZoomBar = true;
        tbZoom.Value = 0;
        isSettingZoomBar = false;
    }

    /*   private GeometryInfo GetGeometryInfo(SqlGeometry geometry)
       {
           OpenGisGeometryType geometryType = SqlGeometryHelper.GetGeometryType(geometry);

           GeometryInfo info = new GeometryInfo() { Type = geometryType };

           switch (geometryType)
           {
               case OpenGisGeometryType.Point:
               case OpenGisGeometryType.MultiPoint:
                   info.Points.AddRange(this.GetGeometryPoints(geometry));

                   break;
               case OpenGisGeometryType.LineString:
                   info.Points.AddRange(this.GetGeometryPoints(geometry));

                   break;
               case OpenGisGeometryType.Polygon:
                   info.Items = this.GetPolygonItems(geometry);

                   break;
               case OpenGisGeometryType.MultiLineString:
                   int num = geometry.STNumGeometries().Value;

                   for (int i = 1; i <= num; i++)
                   {
                       var geom = geometry.STGeometryN(i);

                       info.Items.Add(new GeometryInfoItem() { Points = this.GetGeometryPoints(geom) });
                   }

                   break;
               case OpenGisGeometryType.MultiPolygon:
                   int polygonNum = geometry.STNumGeometries().Value;

                   for (int i = 1; i <= polygonNum; i++)
                   {
                       info.Collection.Add(new GeometryInfo() { Type = OpenGisGeometryType.Polygon, Items = this.GetPolygonItems(geometry.STGeometryN(i)) });
                   }

                   break;
               case OpenGisGeometryType.GeometryCollection:
                   int geomNum = geometry.STNumGeometries().Value;

                   for (int i = 1; i <= geomNum; i++)
                   {
                       var geom = geometry.STGeometryN(i);

                       info.Collection.Add(this.GetGeometryInfo(geom));
                   }

                   break;
           }

           return info;
       }

       private GeometryInfo GetGeometryInfo(SqlGeography geography)
       {
           OpenGisGeometryType geometryType = SqlGeographyHelper.GetGeometryType(geography);

           GeometryInfo info = new GeometryInfo() { Type = geometryType };

           switch (geometryType)
           {
               case OpenGisGeometryType.Point:
               case OpenGisGeometryType.MultiPoint:
                   info.Points.AddRange(this.GetGeometryPoints(geography));

                   break;
               case OpenGisGeometryType.LineString:
                   info.Points.AddRange(this.GetGeometryPoints(geography));
                   break;

               case OpenGisGeometryType.Polygon:
                   info.Items = this.GetPolygonItems(geography);

                   break;
               case OpenGisGeometryType.MultiLineString:
                   int num = geography.STNumGeometries().Value;

                   for (int i = 1; i <= num; i++)
                   {
                       var geom = geography.STGeometryN(i);

                       info.Items.Add(new GeometryInfoItem() { Points = this.GetGeometryPoints(geom) });
                   }

                   break;
               case OpenGisGeometryType.MultiPolygon:
                   int polygonNum = geography.STNumGeometries().Value;

                   for (int i = 1; i <= polygonNum; i++)
                   {
                       info.Collection.Add(new GeometryInfo() { Type = OpenGisGeometryType.Polygon, Items = this.GetPolygonItems(geography.STGeometryN(i)) });
                   }

                   break;
               case OpenGisGeometryType.GeometryCollection:
                   int geomNum = geography.STNumGeometries().Value;

                   for (int i = 1; i <= geomNum; i++)
                   {
                       var geom = geography.STGeometryN(i);

                       info.Collection.Add(this.GetGeometryInfo(geom));
                   }

                   break;
           }

           return info;
       }

       private List<PointF> GetGeometryPoints(SqlGeometry geometry)
       {
           int pointNum = geometry.STNumPoints().Value;

           List<PointF> points = new List<PointF>();

           for (int i = 1; i <= pointNum; i++)
           {
               SqlGeometry point = geometry.STPointN(i);

               points.Add(new PointF((float)point.STX.Value, (float)point.STY.Value));
           }

           return points;
       }

       private List<PointF> GetGeometryPoints(SqlGeography geography)
       {
           int pointNum = geography.STNumPoints().Value;

           List<PointF> points = new List<PointF>();

           for (int i = 1; i <= pointNum; i++)
           {
               SqlGeography point = geography.STPointN(i);

               points.Add(new PointF((float)point.Long.Value, (float)point.Lat.Value * -1));
           }

           return points;
       }

       private List<GeometryInfoItem> GetPolygonItems(SqlGeometry geometry)
       {
           List<GeometryInfoItem> infoItems = new List<GeometryInfoItem>();

           var exteriorRing = geometry.STExteriorRing();

           List<PointF> exteriorPoints = this.GetGeometryPoints(exteriorRing);

           GeometryInfoItem item = new GeometryInfoItem() { Points = exteriorPoints };

           infoItems.Add(item);

           var interiorNum = geometry.STNumInteriorRing().Value;

           for (int i = 1; i <= interiorNum; i++)
           {
               infoItems.Add(new GeometryInfoItem() { Points = this.GetGeometryPoints(geometry.STInteriorRingN(i)) });
           }

           return infoItems;
       }

       private List<GeometryInfoItem> GetPolygonItems(SqlGeography geography)
       {
           List<GeometryInfoItem> infoItems = new List<GeometryInfoItem>();

           var ringNum = geography.NumRings();

           for (int i = 1; i <= ringNum; i++)
           {
               infoItems.Add(new GeometryInfoItem() { Points = this.GetGeometryPoints(geography.RingN(i)) });
           }

           return infoItems;
       }*/

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void ShowGeometry(string content)
    {
        if (rbGeometry.Checked)
        {
            SqlGeometry geom = null;

            try
            {
                geom = SqlGeometry.STGeomFromText(new SqlChars(content), 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (geom.IsNull)
            {
                MessageBox.Show("The geometry is null.");
                return;
            }

            if (!geom.STIsValid())
            {
                MessageBox.Show("The content is invalid.");
                return;
            }

            if (geom.STNumPoints().Value == 0) MessageBox.Show("This a empty geometry.");

            //this.geomInfo = this.GetGeometryInfo(geom);

            //this.DrawGeometry(this.geomInfo);
        }
        else if (rbGeography.Checked)
        {
            SqlGeography geography = null;

            try
            {
                geography = SqlGeography.STGeomFromText(new SqlChars(content), 4326);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (geography.IsNull)
            {
                MessageBox.Show("The geography is null.");
                return;
            }

            if (!geography.STIsValid())
            {
                MessageBox.Show("The content is invalid.");
                return;
            }

            if (geography.STNumPoints().Value == 0)
            {
                MessageBox.Show("This a empty geography.");
            }

            //this.geomInfo = this.GetGeometryInfo(geography);

            //this.DrawGeometry(this.geomInfo);
        }
    }

    private void DrawGeometry(GeometryInfo info, bool isContinuous = false)
    {
        if (!isContinuous) DrawCoordinate();

        if (info == null) return;

        switch (info.Type)
        {
            case OpenGisGeometryType.Point:
            case OpenGisGeometryType.MultiPoint:
                DrawPoints(info.Points);
                break;
            case OpenGisGeometryType.LineString:
                DrawLineString(info.Points);
                break;
            case OpenGisGeometryType.MultiLineString:
                DrawMultiLineString(info);
                break;
            case OpenGisGeometryType.Polygon:
                DrawPolygon(info);
                break;
            case OpenGisGeometryType.MultiPolygon:
                DrawMultiPolygon(info);
                break;
            case OpenGisGeometryType.GeometryCollection:
                var collection = info.Collection;

                foreach (var gi in collection) DrawGeometry(gi, true);
                break;
        }
    }

    private Graphics GetGraphics()
    {
        var g = Graphics.FromImage(picGeometry.Image);
        g.SmoothingMode = SmoothingMode.HighQuality;
        return g;
    }

    private void DrawCoordinate()
    {
        var viewportInfo = GetViewportInfo();

        if (viewportInfo.Width == 0 || viewportInfo.Height == 0) return;

        var img = new Bitmap((int)viewportInfo.Width, (int)viewportInfo.Height);
        var g = Graphics.FromImage(img);

        g.TranslateTransform(viewportInfo.MaxX, viewportInfo.MaxY);

        var pen = new Pen(new SolidBrush(Color.LightGray), 1);
        pen.DashStyle = DashStyle.Dash;

        g.DrawLine(pen, -viewportInfo.MaxX, 0, viewportInfo.MaxX, 0);
        g.DrawLine(pen, 0, -viewportInfo.MaxY, 0, viewportInfo.MaxY);

        picGeometry.Image = img;

        DisposeGraphics(g);
    }

    private GeometryViewportInfo GetViewportInfo()
    {
        return new GeometryViewportInfo
            { Width = panelContent.ClientSize.Width, Height = panelContent.ClientSize.Height };
    }

    private void TranslateTransform(Graphics g)
    {
        var viewport = GetViewportInfo();

        g.TranslateTransform(viewport.MaxX, viewport.MaxY);
    }

    private void DrawPoints(List<PointF> points)
    {
        var g = GetGraphics();

        TranslateTransform(g);

        var viewport = GetViewportInfo();

        foreach (var point in points)
            if (Math.Abs(point.X) <= viewport.MaxX && Math.Abs(point.Y) <= viewport.MaxY)
            {
                var font = new Font(Font.FontFamily, 12, FontStyle.Bold);

                g.DrawRectangle(new Pen(new SolidBrush(Color.Red), 2), new Rectangle((int)point.X, (int)point.Y, 1, 1));
            }
            else
            {
                MessageBox.Show($"The point ({point.X},{point.Y}) not in the viewport.");
            }

        DisposeGraphics(g);
    }

    private void CheckScale(Graphics g)
    {
        if (scale.HasValue && scale.Value > 0) g.ScaleTransform(scale.Value, scale.Value);
    }

    private void DrawLineString(List<PointF> points)
    {
        var g = GetGraphics();

        TranslateTransform(g);

        CheckScale(g);

        g.DrawLines(linePen, points.ToArray());

        DisposeGraphics(g);
    }

    private void DrawMultiLineString(GeometryInfo info)
    {
        var g = GetGraphics();

        TranslateTransform(g);

        CheckScale(g);

        foreach (var item in info.Items) g.DrawLines(linePen, item.Points.ToArray());

        DisposeGraphics(g);
    }

    private void DrawPolygon(GeometryInfo info)
    {
        var g = GetGraphics();

        var allPoints = info.Items.SelectMany(item => item.Points).ToList();

        TranslateAndScale(g, allPoints);

        var count = 0;

        foreach (var item in info.Items)
        {
            g.DrawPolygon(polygonPen, item.Points.ToArray());

            g.FillPolygon(count == 0 ? polygonPen.Brush : new SolidBrush(Color.White), item.Points.ToArray());

            count++;
        }

        DisposeGraphics(g);
    }

    private void TranslateAndScale(Graphics g, List<PointF> points)
    {
        var minPointX = points.Min(item => item.X);
        var minPointY = points.Min(item => item.Y);
        var maxPointX = points.Max(item => item.X);
        var maxPointY = points.Max(item => item.Y);

        var maxDistanceX = Math.Abs(maxPointX - minPointX);
        var maxDistanceY = Math.Abs(maxPointY - minPointY);

        var isNear180X = false;

        if (Math.Abs(minPointX + maxPointX) < Math.Abs(minPointX) + Math.Abs(maxPointX))
            if (rbGeography.Checked)
            {
                isNear180X = points.Any(item => 180 - Math.Abs(maxPointX) <= 10);

                minPointX = points.Where(item => item.X < 0).Max(item => item.X);
                maxPointX = points.Where(item => item.X > 0).Min(item => item.X);

                maxDistanceX = 360 - Math.Abs(minPointX) - Math.Abs(maxPointX);
            }

        if (Math.Abs(minPointY + maxPointY) < Math.Abs(minPointY) + Math.Abs(maxPointY))
            if (rbGeography.Checked)
            {
                minPointY = points.Where(item => item.Y < 0).Max(item => item.Y);
                maxPointY = points.Where(item => item.Y > 0).Min(item => item.Y);

                maxDistanceY = 180 - Math.Abs(minPointY) - Math.Abs(maxPointY);
            }

        var viewport = GetViewportInfo();

        float scale;

        if (!this.scale.HasValue || this.scale.Value == 0)
        {
            scale = Math.Max(viewport.Width / maxDistanceX, viewport.Height / maxDistanceY);

            if (scale > defaultLimitMaxScale)
                scale = defaultLimitMaxScale;
            else
                scale = scale * 0.7f;
        }
        else
        {
            scale = this.scale.Value;
        }

        if (scale <= 0) scale = 1;

        var centerRelativeX = (maxPointX - minPointX) / 2;
        var centerRelativeY = (maxPointY - minPointY) / 2;

        var centerRelativePoint = new PointF(centerRelativeX, centerRelativeY);
        var centerAbsolutePoint = new PointF(centerRelativePoint.X + minPointX, centerRelativePoint.Y + minPointY);

        if (isNear180X)
        {
            if (centerAbsolutePoint.X + 180 > 180)
                centerAbsolutePoint.X -= 180;
            else
                centerAbsolutePoint.X += 180;
        }

        var translateX = (centerAbsolutePoint.X > 0 ? -1 : 1) * Math.Abs(centerAbsolutePoint.X) * scale + viewport.MaxX;
        var translateY = (centerAbsolutePoint.Y > 0 ? -1 : 1) * Math.Abs(centerAbsolutePoint.Y) * scale + viewport.MaxY;

        g.TranslateTransform(translateX, translateY);

        g.ScaleTransform(scale, scale);

        isSettingZoomBar = true;
        tbZoom.Value = (int)scale;
        isSettingZoomBar = false;
    }

    private void DrawMultiPolygon(GeometryInfo info)
    {
        var g = GetGraphics();

        var allPoints = info.Collection.SelectMany(item => item.Items).SelectMany(item => item.Points).ToList();

        TranslateAndScale(g, allPoints);

        foreach (var gi in info.Collection)
        {
            var count = 0;

            foreach (var item in gi.Items)
            {
                g.DrawPolygon(polygonPen, item.Points.ToArray());

                g.FillPolygon(count == 0 ? polygonPen.Brush : new SolidBrush(Color.White), item.Points.ToArray());

                count++;
            }
        }

        DisposeGraphics(g);
    }

    private void DisposeGraphics(Graphics g)
    {
        if (g != null) g.Dispose();
    }

    private void rbGeography_CheckedChanged(object sender, EventArgs e)
    {
        SwitchMode();
    }

    private void SwitchMode()
    {
        var content = txtContent.Text.Trim();

        if (!string.IsNullOrEmpty(content)) ShowGeometry(content);
    }

    private void tbZoom_ValueChanged(object sender, EventArgs e)
    {
        if (isSettingZoomBar) return;

        scale = tbZoom.Value;

        DrawGeometry(geomInfo);
    }

    private void picGeometry_SizeChanged(object sender, EventArgs e)
    {
        if (geomInfo != null)
        {
            ResetScale();

            DrawGeometry(geomInfo);
        }
    }
}

internal class GeometryInfo
{
    internal List<GeometryInfo> Collection = new();

    internal List<PointF> Points = new();
    internal OpenGisGeometryType Type { get; set; }
    internal List<GeometryInfoItem> Items { get; set; } = new();
}

internal class GeometryInfoItem
{
    internal List<PointF> Points = new();
}

internal struct GeometryViewportInfo
{
    internal float Width { get; set; }
    internal float Height { get; set; }
    internal float MaxX => Width / 2;
    internal float MaxY => Height / 2;
}
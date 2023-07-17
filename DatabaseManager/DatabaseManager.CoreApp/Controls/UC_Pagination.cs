using System;
using System.Windows.Forms;

namespace DatabaseManager.Controls;

public partial class UC_Pagination : UserControl
{
    public delegate void PageNumberChangeHandler(long pageNum);

    private const int defaultPageSize = 10;
    private bool isSetting;
    private long pageCount;
    private long pageNum = 1;

    private int pageSize = 10;
    private long totalCount;

    public UC_Pagination()
    {
        InitializeComponent();
        PageNum = 1;
    }

    public int PageSize
    {
        get
        {
            if (int.TryParse(cboPageSize.Text, out pageSize))
                pageSize = int.Parse(cboPageSize.Text);
            else
                pageSize = defaultPageSize;

            return pageSize;
        }
        set
        {
            pageSize = value;

            cboPageSize.Text = pageSize.ToString();
        }
    }

    public long PageCount
    {
        get => pageCount;
        set
        {
            pageCount = value;

            var currentPageNum = cboPageNum.Text;
            isSetting = true;
            cboPageNum.Items.Clear();

            for (var i = 1; i <= pageCount; i++)
            {
                cboPageNum.Items.Add(i.ToString());

                if (i.ToString() == currentPageNum) cboPageNum.SelectedItem = i.ToString();
            }

            if (pageNum > pageCount) PageNum = pageCount;

            if (pageCount == 0) btnFirst.Enabled = btnPrevious.Enabled = btnNext.Enabled = btnLast.Enabled = false;

            isSetting = false;
            lblPageCount.Text = pageCount.ToString();
        }
    }

    public long PageNum
    {
        get
        {
            if (long.TryParse(cboPageNum.Text, out pageNum))
                pageNum = long.Parse(cboPageNum.Text);
            else
                pageNum = 1;

            return pageNum;
        }
        set
        {
            pageNum = value;

            if (pageNum < 1) pageNum = 1;
            cboPageNum.Text = pageNum.ToString();
        }
    }

    public long TotalCount
    {
        get => totalCount;
        set
        {
            totalCount = value;
            lblTotalCount.Text = $"Total:{totalCount}";

            if (pageSize != 0)
                PageCount = totalCount % pageSize == 0 ? totalCount / pageSize : totalCount / pageSize + 1;
            else
                PageCount = 0;
        }
    }

    public event PageNumberChangeHandler OnPageNumberChanged;

    private void cboPageSize_TextChanged(object sender, EventArgs e)
    {
        if (int.TryParse(cboPageSize.Text, out pageSize))
        {
            pageSize = int.Parse(cboPageSize.Text);

            var pCount = totalCount % pageSize == 0 ? totalCount / pageSize : totalCount / pageSize + 1;

            if (pageNum > pCount)
                PageNum = pCount;
            else
                ToPage(pageNum);
        }
    }

    private void btnFirst_Click(object sender, EventArgs e)
    {
        PageNum = 1;
    }

    private void btnPrevious_Click(object sender, EventArgs e)
    {
        if (pageNum > 1) PageNum--;
    }

    private void btnNext_Click(object sender, EventArgs e)
    {
        if (pageNum < pageCount) PageNum++;
    }

    private void btnLast_Click(object sender, EventArgs e)
    {
        PageNum = pageCount;
    }

    private void ToPage(long pageNum)
    {
        btnFirst.Enabled = pageNum != 1;
        btnPrevious.Enabled = pageNum > 1;
        btnNext.Enabled = pageNum < pageCount;
        btnLast.Enabled = pageNum != pageCount;

        if (OnPageNumberChanged != null)
            if (!isSetting)
                OnPageNumberChanged(pageNum);
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        if (OnPageNumberChanged != null) OnPageNumberChanged(pageNum);
    }

    private void cboPageNum_SelectedValueChanged(object sender, EventArgs e)
    {
        if (long.TryParse(cboPageNum.Text, out pageNum))
        {
            pageNum = long.Parse(cboPageNum.Text);

            ToPage(pageNum);
        }
    }

    private void cboPageNum_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
            if (long.TryParse(cboPageNum.Text, out pageNum))
            {
                if (pageNum < 1)
                    PageNum = 0;
                else if (pageNum > pageCount)
                    PageNum = pageCount;
                else
                    PageNum = pageNum;
            }
    }
}
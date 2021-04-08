using OpenDBDiff.Abstractions.Schema.Model;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace OpenDBDiff.UI
{
    public partial class DataCompareForm : Form
    {
        public DataCompareForm(ISchemaBase Selected, string SrcConnectionString, string DestConnectionString)
        {
            InitializeComponent();
            this.selected = Selected;
            this.srcConnectionString = SrcConnectionString;
            this.destConnectionString = DestConnectionString;

            doCompare();
        }

        private void doCompare()
        {
            DataTable srcTable = Updater.getData(selected, srcConnectionString);
            DataTable destTable = Updater.getData(selected, destConnectionString);

            if (HideIdenticalCheckBox.Checked)
            {
                // Remove Identical rows
                var comp = DataRowComparer.Default;
                for (int i = 0; i < srcTable.Rows.Count; i++)
                {
                    foreach (DataRow r2 in destTable.Rows)
                    {
                        var r = srcTable.Rows[i];
                        if (comp.Equals(r, r2))
                        {
                            srcTable.Rows.RemoveAt(i);
                            destTable.Rows.Remove(r2);
                            i--;
                            break;
                        }
                    }
                }
            }
                       
            srcDgv.MultiSelect = false;
            srcDgv.ReadOnly = true;
            srcDgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            srcDgv.RowHeadersVisible = false;
            srcDgv.DataSource = srcTable; //.AsEnumerable().Except(destTable.AsEnumerable());
            srcDgv.Rows[0].Cells[0].Style.ForeColor = Color.Blue;
            srcDgv.CellFormatting += new DataGridViewCellFormattingEventHandler(srcDgv_CellFormatting);


            destDgv.MultiSelect = false;
            destDgv.ReadOnly = true;
            destDgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            destDgv.RowHeadersVisible = false;
            destDgv.DataSource = destTable;
            destDgv.CellFormatting += new DataGridViewCellFormattingEventHandler(destDgv_CellFormatting);
            
        }

        private void destDgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataTable srcTable = (DataTable)srcDgv.DataSource;
            DataTable dstTable = (DataTable)destDgv.DataSource;
            int dstIdx = e.RowIndex;
            if (dstIdx < dstTable.Rows.Count)
            {
                if (dstTable.Rows[dstIdx].RowState == DataRowState.Added)
                {
                    e.CellStyle.ForeColor = Color.Green;
                }
                else if (dstTable.Rows[dstIdx].RowState == DataRowState.Modified)
                {
                    e.CellStyle.ForeColor = Color.Blue;
                }

                // find corresponding rowindex with PK // Assumes first column is PK...
                var pk = dstTable.Rows[dstIdx][0];
                int srcIdx;
                for (srcIdx = 0; srcIdx < srcTable.Rows.Count; srcIdx++)
                    if (pk.Equals(srcTable.Rows[srcIdx][0]))
                        break;

                SetChangedCellStyle(srcIdx, dstIdx, e);                               
                
            }
        }

        private void srcDgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataTable srcTable = (DataTable)srcDgv.DataSource;
            DataTable dstTable = (DataTable)destDgv.DataSource;
            int srcIdx = e.RowIndex;
            if (srcIdx < srcTable.Rows.Count)
            {
                // find corresponding rowindex with PK // Assumes first column is PK... use: pkIdx = dstTable.PrimaryKey[0].Ordinal;
                var pk = srcTable.Rows[srcIdx][0];
                int dstIdx;
                for (dstIdx = 0; dstIdx < dstTable.Rows.Count; dstIdx++)
                    if (pk.Equals(dstTable.Rows[dstIdx][0]))
                        break;                               

                SetChangedCellStyle(srcIdx, dstIdx, e);
            }
        }

        private void SetChangedCellStyle(int srcIdx, int dstIdx, DataGridViewCellFormattingEventArgs e)
        {
            DataTable srcTable = (DataTable)srcDgv.DataSource;
            DataTable dstTable = (DataTable)destDgv.DataSource;

            bool areEqual = (srcIdx < srcTable.Rows.Count && dstIdx < dstTable.Rows.Count);
            if (areEqual)
            {
                var s = srcTable.Rows[srcIdx][e.ColumnIndex];
                var d = dstTable.Rows[dstIdx][e.ColumnIndex];
                if (s == null || d == null)
                    areEqual = (s == d);
                else
                    areEqual = s.Equals(d);
            }

            if (areEqual)
            {
                e.CellStyle.BackColor = Color.White;
            }
            else
            {
                e.CellStyle.BackColor = Color.LightSalmon;
            }

        }

        private void btnCommitChanges_Click(object sender, EventArgs e)
        {
            DataTable destination = (DataTable)destDgv.DataSource;
            DataTable edits = destination.GetChanges();
            if (Updater.CommitTable(edits, selected.FullName, destConnectionString))
            {
                destination.AcceptChanges();
                doCompare();
                btnCommitChanges.Enabled = false;
            }
        }

        private void btnUpdateRow_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            object[] sourceItems = source.Rows[srcDgv.CurrentRow.Index].ItemArray;

            for (int i = 0; i < destination.Columns.Count; i++)
            {
                if (destination.Columns[i].Unique)
                {
                    if (destination.Rows.Find(sourceItems[i]) == null && destination.Columns[i].AutoIncrement)
                    {
                        sourceItems[i] = null;
                    }
                }
            }

            destination.BeginLoadData();
            destination.LoadDataRow(sourceItems, false);
            destination.EndLoadData();
            btnCommitChanges.Enabled = true;
        }

        private void btnMerge_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            destination.Merge(source, true);
            foreach (DataRow dr in destination.Rows)
            {
                if (dr.RowState == DataRowState.Unchanged)
                {
                    dr.SetAdded();
                }
            }
            btnCommitChanges.Enabled = true;
        }

        private void btnRowToRow_Click(object sender, EventArgs e)
        {
            DataTable source = (DataTable)srcDgv.DataSource;
            DataTable destination = (DataTable)destDgv.DataSource;

            DataRow sourceRow = source.Rows[srcDgv.CurrentRow.Index];
            DataRow destinationRow = destination.Rows[destDgv.CurrentRow.Index];

            for (int i = 0; i < destination.Columns.Count; i++)
            {
                if (!destination.Columns[i].Unique)
                {
                    destinationRow[i] = sourceRow[i];
                }
            }
            btnCommitChanges.Enabled = true;
        }
        private ISchemaBase selected;
        private string srcConnectionString;
        private string destConnectionString;

        private void HideIdenticalCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            doCompare();
        }
    }
}

using iText.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PDFManager
{
    /// <summary>
    /// References:
    /// https://kb.itextpdf.com/home/it7kb/examples/splitting-a-pdf-file
    /// https://kb.itextpdf.com/home/it7kb/examples/merging-documents-and-create-a-table-of-contents
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            lblMergeOutput.Content = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Merged File.pdf");
            ((INotifyCollectionChanged)lstMergeFiles.Items).CollectionChanged += (s, e) => UpdateMergeButtonStates();
            lstMergeFiles.SelectionChanged += (s, e) => UpdateMergeButtonStates();
        }

        private void UpdateMergeButtonStates()
        {
            bool moreThanOne = lstMergeFiles.Items.Count > 1;
            bool hasSelection = lstMergeFiles.SelectedIndex >= 0;
            btnRunMerge.IsEnabled = moreThanOne;
            btnRemoveFileMerge.IsEnabled = hasSelection;
            btnMoveUp.IsEnabled = moreThanOne && hasSelection;
            btnMoveDown.IsEnabled = moreThanOne && hasSelection;
        }

        private async void btnRunSplit_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = lblSplitFileSource.Content?.ToString();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                MessageBox.Show("Please select a valid PDF file to split.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSplitPageNum.Text, out int pageNum) || pageNum < 1)
            {
                MessageBox.Show("Please enter a valid page number greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnRunSplit.IsEnabled = false;
                btnRunSplit.Content = "Splitting...";

                await Task.Run(() => SplitPdfFile(sourcePath, pageNum));

                MessageBox.Show("PDF split successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error splitting PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRunSplit.IsEnabled = true;
                btnRunSplit.Content = "Split";
            }
        }

        private void SplitPdfFile(string sourcePath, int pageNum)
        {
            string folderPath = Path.GetDirectoryName(sourcePath);
            string splitDest = Path.Combine(folderPath, "SplitDoc_{0}.pdf");

            using (var pdfDoc = new PdfDocument(new PdfReader(sourcePath)))
            {
                int totalPages = pdfDoc.GetNumberOfPages();
                if (pageNum >= totalPages)
                    throw new ArgumentOutOfRangeException($"Page number {pageNum} is out of range. Document has {totalPages} pages.");

                IList<PdfDocument> splitDocuments = new CustomPdfSplitter(pdfDoc, splitDest).SplitByPageNumbers(new int[] { pageNum });
                foreach (PdfDocument doc in splitDocuments)
                    doc.Close();
            }
        }

        private class CustomPdfSplitter : PdfSplitter
        {
            private readonly string dest;
            private int fileNumber = 1;

            public CustomPdfSplitter(PdfDocument pdfDocument, string dest) : base(pdfDocument)
            {
                this.dest = dest;
            }

            protected override PdfWriter GetNextPdfWriter(PageRange documentPageRange)
            {
                return new PdfWriter(string.Format(dest, fileNumber++));
            }
        }

        private async void btnRunMerge_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = lblMergeOutput.Content?.ToString();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("Please specify an output file path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
            {
                MessageBox.Show("The output directory does not exist.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceFiles = lstMergeFiles.Items.Cast<string>().ToList();
            var missingFiles = sourceFiles.Where(f => !File.Exists(f)).ToList();
            if (missingFiles.Any())
            {
                MessageBox.Show($"The following files no longer exist:\n{string.Join("\n", missingFiles)}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnRunMerge.IsEnabled = false;
                btnRunMerge.Content = "Merging...";

                await Task.Run(() => MergePdfFiles(outputPath, sourceFiles));

                MessageBox.Show("PDFs merged successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error merging PDFs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRunMerge.Content = "Merge";
                UpdateMergeButtonStates();
            }
        }

        private void MergePdfFiles(string outputPath, List<string> sourceFiles)
        {
            using (var resultDoc = new PdfDocument(new PdfWriter(outputPath)))
            {
                var formCopier = new PdfPageFormCopier();
                int pageIndex = 1;

                foreach (var filePath in sourceFiles)
                {
                    using (var srcDoc = new PdfDocument(new PdfReader(filePath)))
                    {
                        int numberOfPages = srcDoc.GetNumberOfPages();
                        srcDoc.CopyPagesTo(1, numberOfPages, resultDoc, formCopier);

                        var rootOutline = resultDoc.GetOutlines(false);
                        var outline = rootOutline.AddOutline(Path.GetFileNameWithoutExtension(filePath));
                        outline.AddDestination(PdfExplicitDestination.CreateFit(resultDoc.GetPage(pageIndex)));

                        pageIndex += numberOfPages;
                    }
                }
            }
        }

        private void btnAddFileMerge_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    if (!lstMergeFiles.Items.Contains(file))
                        lstMergeFiles.Items.Add(file);
                }
            }
        }

        private void btnRemoveFileMerge_Click(object sender, RoutedEventArgs e)
        {
            while (lstMergeFiles.SelectedItems.Count > 0)
                lstMergeFiles.Items.Remove(lstMergeFiles.SelectedItems[0]);
        }

        private void btnBrowseMergeOutput_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "Merged File.pdf",
                DefaultExt = ".pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
                lblMergeOutput.Content = saveFileDialog.FileName;
        }

        private void btnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = lstMergeFiles.SelectedIndex;
            if (index <= 0) return;
            var item = lstMergeFiles.Items[index];
            lstMergeFiles.Items.RemoveAt(index);
            lstMergeFiles.Items.Insert(index - 1, item);
            lstMergeFiles.SelectedIndex = index - 1;
        }

        private void btnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = lstMergeFiles.SelectedIndex;
            if (index < 0 || index >= lstMergeFiles.Items.Count - 1) return;
            var item = lstMergeFiles.Items[index];
            lstMergeFiles.Items.RemoveAt(index);
            lstMergeFiles.Items.Insert(index + 1, item);
            lstMergeFiles.SelectedIndex = index + 1;
        }

        private void openFileSplit_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf"
            };

            if (openFileDialog.ShowDialog() != true) return;

            string path = openFileDialog.FileName;
            lblSplitFileSource.Content = path;
            lblSplitOutputFolder.Content = Path.GetDirectoryName(path);

            try
            {
                using (var pdfDoc = new PdfDocument(new PdfReader(path)))
                {
                    int pages = pdfDoc.GetNumberOfPages();
                    lblSplitPageCount.Content = pages > 1
                        ? $"(enter 1 – {pages - 1}, document has {pages} pages)"
                        : "(document has only 1 page and cannot be split)";
                }
            }
            catch
            {
                lblSplitPageCount.Content = "(unable to read page count)";
            }
        }
    }
}

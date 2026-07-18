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

            string destPattern = GetSplitDestPattern(sourcePath);
            var existingOutputs = new[] { 1, 2 }
                .Select(i => string.Format(destPattern, i))
                .Where(File.Exists)
                .ToList();
            if (existingOutputs.Any())
            {
                var result = MessageBox.Show(
                    $"The following files already exist and will be overwritten:\n{string.Join("\n", existingOutputs)}\n\nContinue?",
                    "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                btnRunSplit.IsEnabled = false;
                btnRunSplit.Content = "Splitting...";

                await Task.Run(() => SplitPdfFile(sourcePath, pageNum));

                MessageBox.Show("PDF split successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (iText.Kernel.Exceptions.BadPasswordException)
            {
                MessageBox.Show("This PDF is password-protected and cannot be split.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (iText.IO.Exceptions.IOException)
            {
                MessageBox.Show("This file is not a valid PDF or is corrupted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private static string GetSplitDestPattern(string sourcePath)
        {
            string folderPath = Path.GetDirectoryName(sourcePath);
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            return Path.Combine(folderPath, baseName + "_part{0}.pdf");
        }

        private void SplitPdfFile(string sourcePath, int pageNum)
        {
            string splitDest = GetSplitDestPattern(sourcePath);

            using (var pdfDoc = new PdfDocument(new PdfReader(sourcePath)))
            {
                int totalPages = pdfDoc.GetNumberOfPages();
                if (totalPages < 2)
                    throw new InvalidOperationException("This document has only 1 page and cannot be split.");
                if (pageNum >= totalPages)
                    throw new InvalidOperationException($"Page number {pageNum} is out of range. The document has {totalPages} pages, so the split point must be between 1 and {totalPages - 1}.");

                // SplitByPageNumbers treats each number as the first page of the next document,
                // so "split after page N" means the next document starts at N + 1
                IList<PdfDocument> splitDocuments = new CustomPdfSplitter(pdfDoc, splitDest).SplitByPageNumbers(new int[] { pageNum + 1 });
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

            if (sourceFiles.Any(f => string.Equals(f, outputPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("The output file cannot be one of the files being merged. Please choose a different output path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (File.Exists(outputPath))
            {
                var result = MessageBox.Show(
                    $"\"{outputPath}\" already exists and will be overwritten.\n\nContinue?",
                    "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
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
                // The writer truncates the output file before merging starts,
                // so a failed merge leaves behind a broken partial PDF
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
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
                    try
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
                    catch (iText.Kernel.Exceptions.BadPasswordException)
                    {
                        throw new InvalidOperationException($"'{Path.GetFileName(filePath)}' is password-protected and cannot be merged.");
                    }
                    catch (iText.IO.Exceptions.IOException)
                    {
                        throw new InvalidOperationException($"'{Path.GetFileName(filePath)}' is not a valid PDF or is corrupted.");
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
                var skipped = new List<string>();
                foreach (var file in openFileDialog.FileNames)
                {
                    if (lstMergeFiles.Items.Cast<string>().Any(f => string.Equals(f, file, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    try
                    {
                        using (new PdfDocument(new PdfReader(file))) { }
                        lstMergeFiles.Items.Add(file);
                    }
                    catch (iText.Kernel.Exceptions.BadPasswordException)
                    {
                        skipped.Add($"{Path.GetFileName(file)} (password-protected)");
                    }
                    catch
                    {
                        skipped.Add($"{Path.GetFileName(file)} (not a valid PDF)");
                    }
                }

                if (skipped.Any())
                    MessageBox.Show($"The following files were not added:\n{string.Join("\n", skipped)}", "Some Files Skipped", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            catch (iText.Kernel.Exceptions.BadPasswordException)
            {
                lblSplitPageCount.Content = "(this PDF is password-protected and cannot be split)";
            }
            catch
            {
                lblSplitPageCount.Content = "(unable to read page count)";
            }
        }
    }
}

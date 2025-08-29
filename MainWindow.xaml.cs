using iText.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Utils;
using System;
using System.Collections.Generic;
using System.IO;
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
        }

        /// <summary>
        /// Split a single PDF document into multiple documents based on a page number
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_Click(object sender, RoutedEventArgs e)
        {
            string splitSourcePath = lblSplitFileSource.Content.ToString();
            PdfDocument pdfDoc = new PdfDocument(new PdfReader(splitSourcePath));

            IList<int> pageNums = new int[] { int.Parse(txtSplitPageNum.Text) }; //Page number to split the document on

            string folderPath = Path.GetDirectoryName(splitSourcePath); //Get the containing folder path
            string splitDest = folderPath + @"\SplitDoc_{0}.pdf";

            IList<PdfDocument> splitDocuments = new CustomPdfSplitter(pdfDoc, splitDest).SplitByPageNumbers(pageNums);

            foreach (PdfDocument doc in splitDocuments)
            {
                doc.Close();
            }

            pdfDoc.Close();
        }

        /// <summary>
        /// Custom PdfSplitter to split PDF documents
        /// </summary>
        private class CustomPdfSplitter : PdfSplitter
        {
            private string dest;
            private int fileNumber = 1; //Counter to keep track of resulting files after the split

            public CustomPdfSplitter(PdfDocument pdfDocument, string dest) : base(pdfDocument)
            {
                this.dest = dest;
            }

            protected override PdfWriter GetNextPdfWriter(PageRange documentPageRange)
            {
                return new PdfWriter(string.Format(dest, fileNumber++));
            }
        }

        /// <summary>
        /// Handle the button click to merge PDF files asynchronously
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button to prevent double-clicks
                btnRunMerge.IsEnabled = false;

                // Run the merge operation in a background task
                await Task.Run(() => MergePdfFiles());

                MessageBox.Show("PDFs merged successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error merging PDFs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the button after the operation is complete
                btnRunMerge.IsEnabled = true;
            }
        }

        /// <summary>
        /// Merge multiple PDF documents into a single document
        /// </summary>
        private void MergePdfFiles()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputPath = Path.Combine(desktopPath, "Merged File.pdf");

            var sourceFiles = new List<string>
            {
                lblFileSourceMerge1.Dispatcher.Invoke(() => lblFileSourceMerge1.Content?.ToString()),
                lblFileSourceMerge2.Dispatcher.Invoke(() => lblFileSourceMerge2.Content?.ToString())
            };

            using (var resultDoc = new PdfDocument(new PdfWriter(outputPath)))
            {
                var formCopier = new PdfPageFormCopier();
                int page = 1;

                foreach (var filePath in sourceFiles)
                {
                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                        continue;

                    using (var srcDoc = new PdfDocument(new PdfReader(filePath)))
                    {
                        int numberOfPages = srcDoc.GetNumberOfPages();
                        for (int i = 1; i <= numberOfPages; i++, page++)
                        {
                            srcDoc.CopyPagesTo(i, i, resultDoc, formCopier);

                            if (i == 1)
                            {
                                var rootOutline = resultDoc.GetOutlines(false);
                                var outline = rootOutline.AddOutline($"p{page}");
                                outline.AddDestination(PdfDestination.MakeDestination(new PdfString($"p{page}")));
                            }
                        }
                    }
                }
            }
        }

        private void openFileSplit_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                lblSplitFileSource.Content = openFileDialog.FileName;
            }
        }

        private void btnOpenFileMerge1_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                lblFileSourceMerge1.Content = openFileDialog.FileName;
            }
        }

        private void btnOpenFileMerge2_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                lblFileSourceMerge2.Content = openFileDialog.FileName;
            }
        }
    }
}

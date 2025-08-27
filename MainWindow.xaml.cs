using iText.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Utils;
using iText.Layout;
using iText.Layout.Element;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// Merge PDF documents
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MemoryStream baos = new MemoryStream();
            PdfDocument pdfDoc = new PdfDocument(new PdfWriter(baos));
            Document doc = new Document(pdfDoc);

            // Copier contains the additional logic to copy acroform fields to a new page.
            // PdfPageFormCopier uses some caching logic which can potentially improve performance
            // in case of the reusing of the same instance.
            PdfPageFormCopier formCopier = new PdfPageFormCopier();

            // Copy all merging file's pages to the temporary pdf file
            List<PdfDocument> filesToMerge = new List<PdfDocument>();

            string mergeSourcePath1 = lblFileSourceMerge1.Content.ToString();
            string mergeSourcePath2 = lblFileSourceMerge2.Content.ToString();

            filesToMerge.Add(new PdfDocument(new PdfReader(mergeSourcePath1)));
            filesToMerge.Add(new PdfDocument(new PdfReader(mergeSourcePath2)));

            int page = 1;

            foreach (PdfDocument entry in filesToMerge)
            {
                // PdfDocument srcDoc = entry.Value;
                int numberOfPages = entry.GetNumberOfPages();

                for (int i = 1; i <= numberOfPages; i++, page++)
                {
                    Text text = new Text(string.Format("Page {0}", page));
                    entry.CopyPagesTo(i, i, pdfDoc, formCopier);

                    // Put the destination at the very first page of each merged document
                    if (i == 1)
                    {
                        text.SetDestination("p" + page);

                        PdfOutline rootOutLine = pdfDoc.GetOutlines(false);
                        PdfOutline outline = rootOutLine.AddOutline("p" + page);
                        outline.AddDestination(PdfDestination.MakeDestination(new PdfString("p" + page)));
                    }

                    doc.Add(new Paragraph(text)
                        .SetFixedPosition(page, 549, 810, 40)
                        .SetMargin(0)
                        .SetMultipliedLeading(1));
                }
            }

            foreach (PdfDocument srcDocument in filesToMerge)
            {
                srcDocument.Close();
            }

            doc.Close();

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            PdfDocument resultDoc = new PdfDocument(new PdfWriter(desktopPath + @"\Merged File.pdf"));
            PdfDocument srcPdfDoc = new PdfDocument(new PdfReader(new MemoryStream(baos.ToArray()), new ReaderProperties()));
            srcPdfDoc.InitializeOutlines();

            List<int> copyPagesOrderList = new List<int>();
            for (int i = 1; i <= srcPdfDoc.GetNumberOfPages(); i++)
            {
                copyPagesOrderList.Add(i);
            }

            srcPdfDoc.CopyPagesTo(copyPagesOrderList, resultDoc, formCopier);

            srcPdfDoc.Close();
            resultDoc.Close();
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

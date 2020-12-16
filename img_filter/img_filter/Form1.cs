﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;

namespace img_filter
{
    public partial class Form1 : Form
    {
        private Mat MyImage;
        private Mat Filter = new Mat();
        private Mat Threshold = new Mat();
        int threshold_Value;

        //확대 축소 관련 변수 
        private System.Drawing.Point LastPoint;
        private double ratio = 1.0F; //확대, 축소 비율
        private System.Drawing.Point imgPoint;
        private Rectangle imgRect;
        private System.Drawing.Point clickPoint;

        DataTable table = new DataTable();

        public Form1()
        {
            InitializeComponent();

            this.button1.Click += new System.EventHandler(this.button1_Click);

            this.comboBox1.Items.AddRange(new object[] {"Blur",
                        "Box",
                        "Median",
                        "Gaussian",
                        "Bilateral"});

            table.Columns.Add("Index", typeof(string));
            table.Columns.Add("X, Y", typeof(string)); //중심점
            table.Columns.Add("Size", typeof(string)); //너비
            table.Columns.Add("GD", typeof(string));   //절대값(바깥과 안쪽의 차이)
            table.Columns.Add("Area", typeof(string)); //면적 
            table.Columns.Add("Max", typeof(string));  //면적중 가장 긴 부분
            table.Columns.Add("Main", typeof(string)); //면적중 가장 짧은 부분
            table.Columns.Add("Mean", typeof(string)); //면적의 평균
            table.Columns.Add("boundingRect", typeof(string));

            dataGridView1.RowHeadersVisible = false; //왼쪽에 뜨는 컬럼창 삭제          
            dataGridView1.DataSource = table;
            dataGridView1.Columns[8].Visible = false;//특정열 안보이게 하기 

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(pictureBox1_DragEnter);

            pictureBox1.MouseWheel += new MouseEventHandler(pictureBox1_MouseWheel);

            imgPoint = new System.Drawing.Point(pictureBox1.Width / 2, pictureBox1.Height / 2);
            imgRect = new Rectangle(0, 0, pictureBox1.Width, pictureBox1.Height); //확대, 축소 이미지를 Handling 
            ratio = 1.0;
            clickPoint = imgPoint;

            pictureBox1.Invalidate();
        }

        //블럭화
        private void button1_Click(object sender, EventArgs e)
        {
            ((DataTable)dataGridView1.DataSource).Rows.Clear(); //Row값만 초기화

            int counter = 1;
            Mat dst = Threshold.Clone();

            double img_area = MyImage.Width * MyImage.Height;


            OpenCvSharp.Point[][] contours; //윤곽선의 실제 값
            HierarchyIndex[] hierarchy;    // 윤곽선들의 계층 구조

            Mat preprocess_Value = new Mat();

            //색상 공간 변환
            Cv2.InRange(Threshold, new Scalar(threshold_Value, threshold_Value, threshold_Value), 
                new Scalar(255, 255, 255), preprocess_Value);

            //Cv2.FindContours(원본 배열, 검출된 윤곽선, 계층 구조, 검색 방법, 근사 방법, 오프셋)
            Cv2.FindContours(preprocess_Value, out contours, out hierarchy, RetrievalModes.Tree, 
                ContourApproximationModes.ApproxTC89KCOS);

            foreach (OpenCvSharp.Point[] p in contours)
            {

                double length = Cv2.ArcLength(p, true); //길이
                double area = Cv2.ContourArea(p, true); //면적

                if (length < 200 || length > 2000) continue; //길이가 너무 작은 윤관석 삭제 

                Rect boundingRect = Cv2.BoundingRect(p); //사각형 계산


                OpenCvSharp.Point[] hull = Cv2.ConvexHull(p, true); //블록
                Moments moments = Cv2.Moments(p, false); //중심점 

                Cv2.Rectangle(dst, boundingRect, Scalar.Red, 2); //사각형 그리기
                                                                 //Cv2.FillConvexPoly(dst, hull, Scalar.Red); //내부 채우기
                                                                 //Cv2.Polylines(dst, new OpenCvSharp.Point[][] { hull }, true, Scalar.Red, 1); //다각형 그리기

                Cv2.DrawContours(dst, new OpenCvSharp.Point[][] { hull }, -1, Scalar.Black, 3); //윤곽석 그리기

               
                double mean = (boundingRect.Width + boundingRect.Height) / 2;

                table.Rows.Add(" " + counter++,
                    " " + (int)(moments.M10 / moments.M00) + ", " + (int)(moments.M01 / moments.M00),
                    " " + Math.Truncate(length * 10) / 10,
                    " " + Math.Abs(area - img_area),
                    " " + area,
                    " " + Math.Max(boundingRect.Width, boundingRect.Height),
                    " " + Math.Min(boundingRect.Width, boundingRect.Height),
                    " " + mean,
                    " " + boundingRect);

            }
            pictureBox2.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(dst);


        }

        //Filter
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;

            switch (index)
            {
                case 0:
                    // 단순 흐림 
                    //입력, 출력, 커널크기, 앵커 (-1, -1)이 중심, 테두리 유형
                    Cv2.Blur(MyImage, Filter, new OpenCvSharp.Size(5, 5), new OpenCvSharp.Point(-1, -1), 
                        BorderTypes.Default);
                    break;
                case 1:
                    //박스 필터
                    Cv2.BoxFilter(MyImage, Filter, MatType.CV_8UC3, new OpenCvSharp.Size(7, 7), 
                        new OpenCvSharp.Point(-1, -1), true, BorderTypes.Default);
                    break;
                case 2:
                    //중간값 흐림
                    Cv2.MedianBlur(MyImage, Filter, 9);
                    break;
                case 3:
                    //가우시안 흐림
                    Cv2.GaussianBlur(MyImage, Filter, new OpenCvSharp.Size(3, 3), 1, 0, BorderTypes.Default);
                    break;
                case 4:
                    //쌍방 필터
                    //과도한 노이즈 필터링이 필요한 오프라인에는 d = 9를 사용하는 것이 좋습니다.
                    //시그마 값이 크면 클수록 만화 처럼 보입니다.(50 ,50)
                    Cv2.BilateralFilter(MyImage, Filter, 9, 50, 50, BorderTypes.Default);
                    break;
            }

            pictureBox2.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(Filter);

        }

        //Threshold
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            //textBox에서 숫자를 전부 지우면 ""값이 들어와서 오류가 발생함
            if (int.TryParse(textBox1.Text, out threshold_Value))
            {
                //형변환 하는 코드이지만 int.TryParse에서 OUT을 이용해 받아가기 때문에 필요없어짐 
                //threshold_Value = Convert.ToInt32(textBox1.Text);  
            }
            else
            {
                threshold_Value = 0;
            }

            Cv2.Threshold(Filter, Threshold, threshold_Value, 255, ThresholdTypes.Binary);

            pictureBox2.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(Threshold);

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            Mat dst = Threshold.Clone();

            Rect boundingRect = (Rect)dataGridView1.Rows[e.RowIndex].Cells[9].Value;

            Cv2.Rectangle(dst, boundingRect, Scalar.Green, 2);
        }

        #region 드래그
        void pictureBox1_DragEnter(object sender, DragEventArgs e)
        {
            var data = e.Data.GetData(DataFormats.FileDrop);

            var fileName = data as string[];

            MyImage = Cv2.ImRead(fileName[0]);

            pictureBox1.Image = Image.FromFile(fileName[0]);


        }
        #endregion

        #region 확대 축소 
        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            int lines = e.Delta * SystemInformation.MouseWheelScrollLines / 120;
            PictureBox pb = (PictureBox)sender;

            if (lines > 0)
            {
                ratio *= 2F;
                if (ratio > 100.0) ratio = 100.0;
            }
            else if (lines < 0)
            {
                ratio *= 0.5F;
                if (ratio < 1) ratio = 1;
            }

            imgRect.Width = (int)Math.Round(pictureBox1.Width * ratio);
            imgRect.Height = (int)Math.Round(pictureBox1.Height * ratio);
            imgRect.X = (int)Math.Round(pb.Width / 2 - imgPoint.X * ratio);
            imgRect.Y = (int)Math.Round(pb.Height / 2 - imgPoint.Y * ratio);

            if (imgRect.X > 0) imgRect.X = 0;
            if (imgRect.Y > 0) imgRect.Y = 0;
            if (imgRect.X + imgRect.Width < pictureBox1.Width) imgRect.X = pictureBox1.Width - imgRect.Width;
            if (imgRect.Y + imgRect.Height < pictureBox1.Height) imgRect.Y = pictureBox1.Height - imgRect.Height;

            pictureBox1.Invalidate();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {       
            if (pictureBox1.Image != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                e.Graphics.DrawImage(pictureBox1.Image, imgRect);
                pictureBox1.Focus();
            }
        }

        
        //마우스에 따른 시점 이동 
        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                clickPoint = new System.Drawing.Point(e.X, e.Y);
            }
            pictureBox1.Invalidate();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                imgRect.X = imgRect.X + (int)Math.Round((double)(e.X - clickPoint.X) / 5);
                if (imgRect.X >= 0) imgRect.X = 0;
                if (Math.Abs(imgRect.X) >= Math.Abs(imgRect.Width - pictureBox1.Width)) imgRect.X = -(imgRect.Width - pictureBox1.Width);
                imgRect.Y = imgRect.Y + (int)Math.Round((double)(e.Y - clickPoint.Y) / 5);
                if (imgRect.Y >= 0) imgRect.Y = 0;
                if (Math.Abs(imgRect.Y) >= Math.Abs(imgRect.Height - pictureBox1.Height)) imgRect.Y = -(imgRect.Height - pictureBox1.Height);
            }
            else
            {
                LastPoint = e.Location;
            }

            imgPoint = new System.Drawing.Point(e.X, e.Y);

            pictureBox1.Invalidate();
        }
        #endregion
    }
}

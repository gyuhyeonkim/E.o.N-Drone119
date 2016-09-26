
#include <stack>
#include <iostream>
#include <time.h>
#include <math.h>
#include <windows.h>
#include "opencv2/opencv.hpp"
#define WIDTH 640
#define HEIGHT 480
#define ROI_DEPART 8

using namespace std;
using namespace cv;

const double MAX_TIME_DELTA = 0.5;
const double MIN_TIME_DELTA = 0.05;
const double MHI_DURATION = 1;
const int BUFFER_N = 4;
static int RES = 0;

void InitFrameBuffer(IplImage *buffer[], CvSize size);
void ReleaseFrameBuffer(IplImage *buffer[]);
void ReleaseFrameBuffer(IplImage *buffer[][ROI_DEPART]);
void DifferenceIFrames(IplImage *img1, IplImage *img2, IplImage *diffImage, int threshold);
void ConvertMHItoMotionImage(IplImage *mhi, IplImage *motion, IplImage *mask, double timeStamp);

int main() {
	CvCapture *capture = cvCreateFileCapture("obstacle.avi");
	if (!capture) return 0;
	int width = (int)cvGetCaptureProperty(capture, CV_CAP_PROP_FRAME_WIDTH);
	int height = (int)cvGetCaptureProperty(capture, CV_CAP_PROP_FRAME_HEIGHT);
	CvSize fsize = cvSize(width, height);


	CvScalar scalar=CV_RGB(255, 0, 0);
	CvMat matHeader;
	int tmpt = 0;
	int t = 0;
	double threshold = 150; // LOW TO HIGH
	double timeStamp;
	int last = 0; int prev, curr;
	char *text = "X";
	CvFont font,font2;
	cvInitFont(&font, CV_FONT_VECTOR0, 0.7, 0.7, 0, 1);
	cvInitFont(&font2, CV_FONT_VECTOR0, 2, 2, 0, 3);
	double fontScale = 5.0;
	int thickness = 1;
	int baseLine;
	
	Point center(width /2, height /2);
	IplImage * grayImage = cvCreateImage(fsize, IPL_DEPTH_8U, 1);
	IplImage * motion = cvCreateImage(fsize, 8, 3);				cvZero(motion);
	IplImage * mask = cvCreateImage(fsize, IPL_DEPTH_8U, 1);		cvZero(mask);			// 이미지 생성 함수
	IplImage * segmask = cvCreateImage(fsize, IPL_DEPTH_32F, 1);	cvZero(segmask);
	IplImage * mhi = cvCreateImage(fsize, IPL_DEPTH_32F, 1);	cvZero(mhi);
	IplImage * tmpImage = cvCreateImage(fsize, IPL_DEPTH_8U, 3); cvZero(tmpImage);
	IplImage * orient = cvCreateImage(fsize, IPL_DEPTH_32F, 1); cvZero(orient);
	IplImage * gray2 = cvCreateImage(fsize, IPL_DEPTH_8U, 1); cvZero(gray2);
	IplImage * SovelImage = cvCreateImage(fsize, IPL_DEPTH_8U, 1); cvZero(SovelImage);
	IplImage * Sovel2Image = cvCreateImage(fsize, IPL_DEPTH_8U, 1); cvZero(Sovel2Image);
	IplImage * tmpgray = cvCreateImage(fsize, IPL_DEPTH_8U, 1); cvZero(tmpgray);
	IplImage * hsvImage = cvCreateImage(fsize, IPL_DEPTH_8U, 3); cvZero(hsvImage);
	IplImage *edgeImage = cvCreateImage(fsize, IPL_DEPTH_8U, 1);
	IplImage *hsv = cvCreateImage(fsize, IPL_DEPTH_8U, 1); cvZero(hsv);

	CvSeq * tSeq[ROI_DEPART][ROI_DEPART];
	for (int i = 0; i < ROI_DEPART; i++) {
		for (int j = 0; j < ROI_DEPART; j++) {
			tSeq[i][j] = NULL;
		}
	}
	CvSeq * seq = NULL;
	CvMemStorage * cStorage = cvCreateMemStorage(0);
	CvMemStorage * storage = cvCreateMemStorage(0);
	CvMemStorage * tStorage[ROI_DEPART][ROI_DEPART];
	CvMemStorage * hStorage[ROI_DEPART][ROI_DEPART];
	for (int i = 0; i < ROI_DEPART; i++) {
		for (int j = 0; j < ROI_DEPART; j++) {
			tStorage[i][j] = cvCreateMemStorage(0);
			hStorage[i][j] = cvCreateMemStorage(0);
		}
	}
	IplImage * buffer[BUFFER_N];
	InitFrameBuffer(buffer, fsize);

	IplImage * frame = NULL;
	IplImage * tmpframe = cvCreateImage(fsize, IPL_DEPTH_32F, 3); cvZero(tmpframe);
	IplImage * silh = NULL;
	IplImage * dstImage[3][3];
	for (int i = 0; i < 3; i++) {
		for (int j = 0; j < 3; j++) {
			dstImage[i][j] = cvCreateImage(fsize, IPL_DEPTH_8U, 3);
			cvZero(dstImage[i][j]);
		}
	}
	CvSeq *contours = 0;
	
	//배경 제거를 위한 평균 픽셀값 계산
	int cycle = 0;
	IplImage *sumImage = cvCreateImage(fsize, IPL_DEPTH_32F, 1);


	cvNamedWindow("2", CV_WINDOW_NORMAL);

	int thres = 100;
	cvCreateTrackbar("thres", "2", &thres, 250);
	//트랙바에서 사용되는 변수 초기화 
	int LowH = 170;
	int HighH = 179;

	int LowS = 50;
	int HighS = 255;

	int LowV = 0;
	int HighV = 255;


	//트랙바 생성 
	cvCreateTrackbar("LowH", "1", &LowH, 179); //Hue (0 - 179)
	cvCreateTrackbar("HighH", "1", &HighH, 179);

	cvCreateTrackbar("LowS", "1", &LowS, 255); //Saturation (0 - 255)
	cvCreateTrackbar("HighS", "1", &HighS, 255);

	cvCreateTrackbar("LowV", "1", &LowV, 255); //Value (0 - 255)
	cvCreateTrackbar("HighV", "1", &HighV, 255);
	while (1) { // 영상 처리 MAIN	
		tmpframe = cvQueryFrame(capture);
		if (!tmpframe) break;

		/* HSV 값 검출 */

		cvCvtColor(tmpframe, hsvImage, CV_BGR2HSV);
	//	cvShowImage("hsv123", hsvImage);
	//	cvInRangeS(hsvImage, cvScalar(LowH, LowS, LowV), cvScalar(HighH, HighS, HighV), hsv);
	//	cvShowImage("hsv", hsv);

	/* filter
			Mat Mat_Frame = cvarrToMat(frame);
			GaussianBlur(Mat_Frame, Mat_Frame,Size(5,5),1.5);
			IplImage *Ipl_bg = new IplImage(Mat_Frame);
	*/		
		
	/* binary image 합쳐
		cvShowImage("pre-thres-gray", grayImage);
		cvThreshold(gray2, gray2, 140, 255, CV_THRESH_BINARY);
		cvLaplace(grayImage, grayImage, 3);
		cvShowImage("post-erode-gray", grayImage);
	*/

	// motion detecting
	//	cvDilate(frame, frame); cvErode(frame, frame); cvDilate(frame, frame); cvDilate(frame, frame);
	
	//	cvCvtColor(tmpframe, gray2, CV_BGR2HSV);
	//	cvSplit(gray2,)


	/* sobel 
		cvSobel(gray2, SovelImage, 1, 0);
		cvSobel(gray2, Sovel2Image, 0, 1);
		Mat Sobel = abs(SovelImage) + abs(Sovel2Image);
		IplImage *sbImage = new IplImage(Sobel);
	*/

		// 객체를 찾고, 
		// 컨투어 길이를 제한하여 사물을 확률적으로 찾고, 카메라 히스토그램 값이 적용된 화면에 아랫단을 가진 물체를 추적한다.
		//가장 움직임이 큰 것이 가장 가깝다고 가정할 수 있다.
		// 외곽선을 찾고 근사화하여, convex hull, convex loss, hue moments 를 이용해 다른 깊이값을 fill, 바닥의 밑점 중심으로 가까운 물체를 찾는다.

		//
	/* contour detection  속도 문제 */
		cvThreshold(edgeImage, edgeImage, 80, 255, CV_THRESH_BINARY);
		cvShowImage("houghdst123", edgeImage);
		cvFindContours(edgeImage, storage, &contours, sizeof(CvContour),CV_RETR_LIST, CV_CHAIN_APPROX_NONE, cvPoint(0, 0));
		if (contours) {
			cvDrawContours(edgeImage, contours, CV_RGB(255, 255, 255), CV_RGB(0, 255, 0), 100, 1, 8);
		}

		
		
		//cvCanny(buffer[last], buffer[last], 200, 150, 3);
	/* DOG, LOG
		IplImage *buffer1 = cvCreateImage(fsize, IPL_DEPTH_8U, 1);
		IplImage *buffer2 = cvCreateImage(fsize, IPL_DEPTH_8U, 1);
		cvSmooth(buffer[last], buffer1, CV_GAUSSIAN, 3, 0, 0, 0);
		cvSmooth(buffer[last], buffer2, CV_GAUSSIAN, 21, 0, 0, 0);
		cvSub(buffer1, buffer2,buffer[last],0);

	*/
		/*
		for (int count = 0; count < 3; count++) {
			cvErode(buffer[last], buffer[last]);
		}

		for (int count = 0; count < 1; count++) {
			cvDilate(buffer[last], buffer[last]);
		}
		*/
		//cvThreshold(buffer[last], buffer[last], 50, 255, CV_THRESH_BINARY_INV);
		//cvShowImage("BUFFER", buffer[last]);
		cvCvtColor(tmpframe, buffer[last],CV_BGR2GRAY);
		curr = last;	
		prev = (curr + 1) % BUFFER_N;
		last = prev;
		silh = buffer[prev];
		if (++t < BUFFER_N) continue;
		DifferenceIFrames(buffer[prev], buffer[curr], silh, 150);
	//	cvShowImage("silh", silh);
		timeStamp = (double)clock() / CLOCKS_PER_SEC;
		cvUpdateMotionHistory(silh, mhi, timeStamp, MHI_DURATION);


	// HOUGH TRANSFORM 
		IplImage *HdstImage = cvCreateImage(fsize, IPL_DEPTH_8U, 3);

		CvMemStorage* Hstorage = cvCreateMemStorage(0);
		CvSeq* seqLines = 0;
		int k;

		/* sobel	
		cvSobel(gray2, SovelImage, 1, 0);
		cvSobel(gray2, Sovel2Image, 0, 1);
		Mat Sobel = abs(SovelImage) + abs(Sovel2Image);
		IplImage *sbImage = new IplImage(Sobel);
		cvCvtColor(sbImage, edgeImage, CV_BGR2GRAY);
		cvShowImage("sb", sbImage);
		*/
		cvThreshold(tmpframe, tmpframe, 35, 255, THRESH_BINARY);
		cvCanny(tmpframe, edgeImage, 15,130,3); 
		//cvDilate(edgeImage, edgeImage); 
		//cvErode(edgeImage, edgeImage);
		cvCvtColor(edgeImage, HdstImage, CV_GRAY2BGR);
		seqLines = cvHoughLines2(edgeImage, Hstorage, CV_HOUGH_STANDARD, 1, CV_PI / 180, 50, 0, 0);
		for (int i = 0; i <	MIN(seqLines->total,300); i++)
		{
			float* line = (float*)cvGetSeqElem(seqLines, i);
			float rho = line[0];
			float theta = line[1];
			CvPoint pt1, pt2;
			double a = cos(theta), b = sin(theta);
			if ((theta * 180 / CV_PI)>30 && (theta * 180 / CV_PI)<150) continue;
			double x0 = a*rho, y0 = b*rho;
			pt1.x = cvRound(x0 + 1000 * (-b));
			pt1.y = cvRound(y0 + 1000 * (a));
			pt2.x = cvRound(x0 - 1000 * (-b));
			pt2.y = cvRound(y0 - 1000 * (a));
			cvLine(HdstImage, pt1, pt2, CV_RGB(0, 255, 0), 1, 8);
		}
		
	
	/* hough lines point line */
 
		seqLines = cvHoughLines2(grayImage, Hstorage, CV_HOUGH_PROBABILISTIC, 1, CV_PI / 180, 80, 30, 10);
		for (int i = 0; i < MIN(seqLines->total,100); i++)
		{
			CvPoint* line = (CvPoint*)cvGetSeqElem(seqLines, i);
				cvLine(HdstImage, line[0], line[1], CV_RGB(255, 0, 255), 1, 8);
		}

		cvPutText(HdstImage, "HoughLine Detec Zone ", cvPoint(width * 1 / 7, height * 9 / 10), &font2, CV_RGB(255, 255, 255));
		cvShowImage("houghdst", HdstImage);
		cvReleaseMemStorage(&Hstorage); 
		
		for (int i = 0; i < ROI_DEPART; i++) {
			for (int j = 0; j < ROI_DEPART; j++) {
				cvClearMemStorage(tStorage[i][j]);
				cvClearMemStorage(hStorage[i][j]);
			}
		}
		for (int i = 0; i < ROI_DEPART-1; i++) {
			for (int j = 0; j < ROI_DEPART; j++) {
				cvSetImageROI(segmask, cvRect(width / ROI_DEPART * j, height / ROI_DEPART* i, width / ROI_DEPART * (j + 1), height / ROI_DEPART * (i + 1)));
				cvSetImageROI(mhi, cvRect(width / ROI_DEPART * j, height / ROI_DEPART * i, width / ROI_DEPART * (j + 1), height / ROI_DEPART * (i + 1)));
				tSeq[i][j] = cvSegmentMotion(mhi, segmask, tStorage[i][j], timeStamp, MHI_DURATION);
				cvResetImageROI(segmask);
				cvResetImageROI(mhi);
			}
		}
	/* LAYOUT */
	frame = cvQueryFrame(capture);
	for (int i = 0; i < ROI_DEPART-1; i++) {
		for (int j = 0; j < ROI_DEPART; j++) {

			if (tSeq[i][j]-> total > 30) {

			//	cout << "#" << ROI_DEPART*i + j + 1 << " tSeq : " << tSeq[i][j]->total << endl;
				cvRectangle(frame, cvPoint(width / ROI_DEPART * j, height / ROI_DEPART * i), cvPoint(width / ROI_DEPART * (j + 1), height / ROI_DEPART * (i + 1)), CV_RGB(140, 0, 0),-1);
				cvPutText(frame, text, cvPoint(width / (ROI_DEPART * 2) * (2 * j + 1), height / (ROI_DEPART * 2) * (2 * i + 1)), &font, CV_RGB(255, 0, 0));

			}
			cvRectangle(frame, cvPoint(width / ROI_DEPART * j, height / ROI_DEPART * i), cvPoint(width / ROI_DEPART * (j + 1), height / ROI_DEPART * (i + 1)), CV_RGB(255, 0, 0));
			cvPutText(frame, "Motion Detec Zone ", cvPoint(width*1 / 7, height*9 / 10), &font2, CV_RGB(255, 255, 255));
			// cvfloodfill();

		}
	}	
	cvShowImage("MainFrame_", frame);
	char chKey = cvWaitKey(10);
	if (chKey == 27) break;

	}

	cvDestroyAllWindows();
	cvReleaseCapture(&capture);
	cvReleaseImage(&frame);
	cvReleaseImage(&grayImage); cvReleaseImage(&motion);
	cvReleaseImage(&mask);	cvReleaseImage(&tmpImage);
	cvReleaseImage(&gray2);	cvReleaseImage(&SovelImage);	cvReleaseImage(&Sovel2Image);
	cvReleaseImage(&tmpgray);
	cvReleaseImage(&segmask);
	cvReleaseImage(&mhi);
	cvReleaseImage(&orient);
	ReleaseFrameBuffer(buffer);
	cvReleaseMemStorage(&storage);
	for (int i = 0; i < ROI_DEPART - 1; i++) {
		for (int j = 0; j < ROI_DEPART; j++) {
			cvReleaseMemStorage(&tStorage[i][j]);
		}
	}
	return 0;
}

/*
프레임 버퍼의 할당 및 초기화
*/ 
void InitFrameBuffer(IplImage *buffer[], CvSize size) {
	int i;
	for (i = 0; i < BUFFER_N; i++) {
		buffer[i] = cvCreateImage(size, IPL_DEPTH_8U, 1);
		cvZero(buffer[i]);
	}
}
/*
프레임 버퍼 데이터 할당 해제
*/
void ReleaseFrameBuffer(IplImage *buffer[]) {
	int i;
	for (i = 0; i < BUFFER_N; i++) {
		cvReleaseImage(&buffer[i]);
	}
}

void ReleaseFrameBuffer(IplImage *buffer[][ROI_DEPART]) {
	int i,j;
	for (int i = 0; i < ROI_DEPART; i++) {
		for (int j = 0; j < ROI_DEPART; j++) {
			cvReleaseImage(&buffer[i][j]);
		}
	}
}
/*
1. img1 과 img2 의 차를 구하여 절대값을 diffiamge에 저장
2. 영상을 이진화한다. 임계값 기준으로 한다.
*/
void DifferenceIFrames(IplImage *img1, IplImage *img2, IplImage *diffImage, int threshold) {
	cvAbsDiff(img1, img2, diffImage);
	cvThreshold(diffImage, diffImage, threshold, 255, CV_THRESH_BINARY);

}

/*
motion 에 mhi 를 스켈링 한 값 mask 값을 넣는다.
*/
void ConvertMHItoMotionImage(IplImage *mhi, IplImage *motion, IplImage *mask, double timeStamp) {
	double scale = 255 / MHI_DURATION;
	double t = MHI_DURATION - timeStamp;
	cvScale(mhi, mask, scale, t*scale); // mask = mhi * scale + t*scale
	cvZero(motion);
	cvMerge(mask, 0, 0, 0, motion);
}

void printMat(const CvMat *mat, const char *strName) {
	int x, y;
	double fvalue;
	printf(" %s = \n", strName);
	for (y = 0; y < mat->rows; y++) {
		for (x = 0; x < mat->cols; x++) {
			fvalue = cvGetReal2D(mat, y, x);
			printf("%4d", cvRound(fvalue));
		}
		printf("\n");
	}printf("\n\n");
}
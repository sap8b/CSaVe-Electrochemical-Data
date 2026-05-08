function polarization_curve_ss138_fitting
%polarization_curve_ss138_fitting - Model parameters using response surface
%approach
%
%The purpose of this function is to fit a model polarization curve to a
%cathodic polarization dataset in order to extract reaction specific
%parameters
%
% Syntax:  polarization_curve_ss138_model2
%
% Inputs:
%
% Outputs:
%
% Example: 
%    Line 1 of example
%    Line 2 of example
%    Line 3 of example
%
% Other m-files required: none
% Subfunctions: none
% MAT-files required: none
%
% See also: LoadPolarizationCurve, ExtractFiles
%
%==========================================================================
% Author:   Steve Policastro, Ph.D., Materials Science
% Center for Corrosion Science and Engineering, U.S. Naval Research
% Laboratory
% email address: steven.policastro@nrl.navy.mil  
% Website: 
% October 2021; Last revision: 17-Feb-2022
%==========================================================================
    clc;
    clear all;
       
    TC = [10.0, 10.0, 40.0, 40.0, 25.0, 25.0, 25.0, 5.0, 45.0]; %C
    TK = TC + 273.15; %K
    c_Cl = [0.1, 4.6, 0.1, 4.6, 0.01, 2.7, 5.2, 2.7, 2.7]; %M 

    oxideEffectPotentials = [-0.34, -0.32, -0.34, -0.32, -0.24, -0.30, -0.30, -0.32, -0.29; ...
        -0.20, -0.30, -0.18, -0.30, -0.20, -0.30, -0.28, -0.30, -0.29];

%     rho_soln = [6296.5, 51.15, 51.15, 12.72, 12.72, 12.72, 5.0, 5.0, 5.0, 2.82, 2.82, 2.52, 2.52];
        
    pH = 7.0;
    area = 0.495; %cm2
    
    % ====================================
    % Computer 1 = Zwift-PC
    % Computer 2 = Gibbs PC
    % ====================================
    computer = 2; %1; %
    userName = {'steve', 'Steve Policastro'};
    base_directory = fullfile('C:','Users',userName{computer},'OneDrive','Atmospheric Corrosion','Matlab Files','Polarization Curve Model'); %,'Polarization Curve Paper'
    base_directory = 'Analysis Model';
    extension = '.xlsx';
%     xml_filename = 'simulation_parameters3';
%     fignums = 1;
    
    s = struct('conc',0.0,'TC',0.0,'dGC',[-1.0, -1.0, -1.0, -1.0],'dGA',[-1.0, -1.0, -1.0, -1.0],'a',[-1.0, -1.0, -1.0, -1.0],'del',[-1.0, -1.0, -1.0, -1.0]);
    
    firstWord = 'Model_13-8_';
    for j = 1:length(TC)
        secondWord = num2str(c_Cl(j)*1000);
        thirdWord = num2str(TC(j));

        fileName = strcat(firstWord,secondWord,'MM','_',thirdWord,'C',extension);
        neededFileNames = char(fullfile(base_directory,fileName));

        if isfile(neededFileNames)
             % File exists.
             T = readtable(neededFileNames);
             s(j).conc = T.Cl_(1);
             s(j).TC = T.T(1);
             for i = 1:4
                 s(j).dGC(i) = T.dG_Cathodic(i);
                 s(j).dGA(i) = T.dG_Anodic(i);
                 s(j).a(i) = T.alpha(i);
                 s(j).del(i) = T.Diffusion_Length(i);                
             end
        else
             % File does not exist.
             disp('Error')
        end        
       
    end    
   
    disp(s)

    L = size(s,2);
    concCl = zeros(size(s));
    temps = zeros(size(s));

    dGC_H = zeros(size(s));
    dGA_H = zeros(size(s));
    a_H= zeros(size(s));
    d_H = zeros(size(s));

    dGC_O = zeros(size(s));
    dGA_O = zeros(size(s));
    a_O= zeros(size(s));
    d_O = zeros(size(s));

    dGC_R = zeros(size(s));
    dGA_R = zeros(size(s));
    a_R= zeros(size(s));
    d_R = zeros(size(s));

    dGC_C = zeros(size(s));
    dGA_C = zeros(size(s));
    a_C = zeros(size(s));
    d_C = zeros(size(s));    

    for i = 1:L        
        temps(i) = s(i).TC;
        concCl(i) = s(i).conc;    
    end

    for i = 1:L
        dGC_H(i) = s(i).dGC(1);
        dGA_H(i) = s(i).dGA(1);
        a_H(i) = s(i).a(1);
        d_H(i) = s(i).del(1);   

        dGC_O(i) = s(i).dGC(2);
        dGA_O(i) = s(i).dGA(2);
        a_O(i) = s(i).a(2);
        d_O(i) = s(i).del(2);   

        dGC_R(i) = s(i).dGC(3);
        dGA_R(i) = s(i).dGA(3);
        a_R(i) = s(i).a(3);
        d_R(i) = s(i).del(3);   

        dGC_C(i) = s(i).dGC(4);
        dGA_C(i) = s(i).dGA(4);
        a_C(i) = s(i).a(4);
        d_C(i) = s(i).del(4);           
    end

    % Setup the modeling....
    fname = 'PolarizationCurveParameters_13.8_pc3.csv';
    C = {' ','A1',' ',' ','A2',' ',' ','A3'};
    headings = strjoin(C,',');
    headings2 = 'a11,a12,a13,a21,a22,a23,a31,a32,a33,Fit';
    allZeros = '0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,100';
    aOne = '1.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,1.0,100';

    fileID = fopen(fname,'w');    
    fprintf(fileID,'%s\n',headings);
    fprintf(fileID,'%s\n',headings2);

    allX2 = 5.0:0.01:45.0;
    allX1 = 0.01:0.01:5.2;

    mdlp = @poly;
    mdlLL = @linear_linear;
    % ===============================
    % Begin HER
    % ===============================
    y1 = [dGC_H(5),dGC_H(6),dGC_H(7)];          %<===============
    y2 = [dGC_H(8),dGC_H(6),dGC_H(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];    
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [dGC_H(1),p1,dGC_H(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0);    

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];    
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [dGC_H(8),p3,dGC_H(6),p4,dGC_H(9)];    %<===============
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [dGC_H(2),p2,dGC_H(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x4,y4,'b+')      
%     plot(allX2,feval(ay5,allX2),'-b')    
%     hold off    

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,allX1);
    yy6 = feval(ay9,allX1);
    yy7 = feval(ay10,allX1);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    
    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
%     val = (yy5 + yy6*(25))/(1.0 + yy7 *25);
    val2 = yy5 + yy6*25 + yy7*25^2;
    actual_val = 149325.156477562;
    fprintf(fileID,aLine);    
    % ===============================
    % ===============================
    y1 = [dGA_H(5),dGA_H(6),dGA_H(7)];          %<===============
    aLine = strcat(num2str(y1(1)),',',allZeros,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % ===============================
    y1 = [0.8,a_H(6),a_H(7)];          %<=============== a_H(5)
    y2 = [a_H(8),a_H(6),a_H(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    p10 = feval(aTC,2.7);

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [a_H(1),p1,a_H(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    p11 = feval(ay2,25.0);

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [a_H(8),p3,a_H(6),p4,a_H(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [a_H(2),p2,a_H(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,2.7);
% 
%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)

    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 0.787835535263228;
    
    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [d_H(5),d_H(6),d_H(7)];          %<===============
    aLine = strcat(aOne,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % End HER
    % Begin ORR
    % ===============================    
    y1 = [1.87e5,182639.698433949,dGC_O(6),dGC_O(7)];          %<=============== dGC_O(5),
    y2 = [dGC_O(8),dGC_O(6),dGC_O(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,0.6,2.7,5.2];      %
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    p10 = feval(aTC,2.7);

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [dGC_O(1),p1,dGC_O(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    p11 = feval(ay2,25.0);

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [dGC_O(8),p3,dGC_O(6),p4,dGC_O(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [dGC_O(2),p2,dGC_O(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,2.7);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)

    val = yy5 + yy6*25 + yy7 *25^2;
    actualVal = 176944.055515685;
    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [dGA_O(5),dGA_O(6),dGA_O(7)];          %<===============
    aLine = strcat(num2str(y1(1)),',',allZeros,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % ===============================
    y1 = [0.874, 0.884806587493968,a_O(6),a_O(7)];          %<=============== a_O(5),
    y2 = [a_O(8),a_O(6),a_O(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,0.6,2.7,5.2];      %
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [a_O(1),p1,a_O(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [a_O(8),p3,a_O(6),p4,a_O(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [a_O(2),p2,a_O(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);
    
%     figure(100)
%     hold on
%     plot(x4,y4,'b+')      
%     plot(allX2,feval(ay4,allX2),'-b')    
%     hold off

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,2.7);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)
    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [0.34,0.409472275335733,d_O(6),d_O(7)];          %<=============== d_O(5)
    y2 = [d_O(8),d_O(6),d_O(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,0.6,2.7,5.2];      
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [d_O(1),p1,d_O(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [d_O(8),p3,d_O(6),p4,d_O(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [d_O(2),p2,d_O(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);
    
    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,allX1);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','4','\n');
    disp(aLine)
    fprintf(fileID,aLine);    
    % ===============================
    % End ORR
    % Begin Rxn1
    % ===============================  
    y1 = [1.764e5,dGC_R(6),dGC_R(7)];          %<=============== dGC_R(5) 172996.708281312,
    y2 = [dGC_R(8),dGC_R(6),dGC_R(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      %0.6,
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [dGC_R(1),p1,dGC_R(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [dGC_R(8),p3,dGC_R(6),p4,dGC_R(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [dGC_R(2),p2,dGC_R(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);
    
    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,allX1);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','4','\n');
    disp(aLine)

    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 172996.708281312;

    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [dGA_R(5),dGA_R(6),dGA_R(7)];          %<===============
    aLine = strcat(num2str(y1(1)),',',allZeros,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % ===============================    
    y1 = [0.8515,a_R(6),a_R(7)];          %<=============== a_R(5) 0.866606636267034,
    y2 = [a_R(8),a_R(6),a_R(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      % 0.6,
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);
    
%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [a_R(1),p1,a_R(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [a_R(8),p3,a_R(6),p4,a_R(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [a_R(2),p2,a_R(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,allX1);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','4','\n');
    disp(aLine)
    
    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 0.866606636267034;

    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [492.507,d_R(6),d_R(7)];          %<=============== d_R(5), 393.170663501887,
    y2 = [d_R(8),d_R(6),d_R(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      % 0.6,
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [d_R(1),p1,d_R(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [d_R(8),p3,d_R(6),p4,d_R(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [d_R(2),p2,d_R(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x4,y4,'b+')      
%     plot(allX2,feval(ay5,allX2),'-b')    
%     hold off
    
    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,2.7);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)
    
    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 393.170663501887;

    fprintf(fileID,aLine);    
    % ===============================
    % End Rxn1
    % Begin Anodic
    % ===============================  
    y1 = [dGC_C(5),dGC_C(6),dGC_C(7)];          %<===============
    aLine = strcat(num2str(y1(1)),',',allZeros,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % ===============================  
    y1 = [dGA_C(5),dGA_C(6),dGA_C(7)];          %<===============
    y2 = [dGA_C(8),dGA_C(6),dGA_C(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [dGA_C(1),p1,dGA_C(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [dGA_C(8),p3,dGA_C(6),p4,dGA_C(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [dGA_C(2),p2,dGA_C(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

%     figure(100)
%     hold on
%     plot(x4,y4,'b+')      
%     plot(allX2,feval(ay4,allX2),'-b')    
%     hold off

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,2.7);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)
    
    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 189110.460272102;

    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [a_C(5),a_C(6),a_C(7)];          %<===============
    y2 = [a_C(8),a_C(6),a_C(9)];          %<===============    

    % Calculate fit along T = 25C
    x1 = [0.01,2.7,5.2];      
    beta0 = [1.0, -0.1, 0.01];
    aTC = fitnlm(x1,y1,mdlp,beta0);
    
%     figure(100)
%     hold on
%     plot(x1,y1,'b+')      
%     plot(allX1,feval(aTC,allX1),'-b')    
%     hold off

    % Get values at C = 0.1M and 4.6M along T = 25C line
    p1 = feval(aTC,0.1);
    p2 = feval(aTC,4.6);
    y3 = [a_C(1),p1,a_C(3)];                %<===============

    % Calculate fit along C = 2.7M
    x2 = [5.0,25.0,45.0];    
    beta0 = [1.0, -0.1, 0.01];
    ay2 = fitnlm(x2,y2,mdlp,beta0); 

    % Get values at T = 10C and 40C along the C = 2.7M line
    p3 = feval(ay2,10.0);
    p4 = feval(ay2,40.0);

    % Get fit along C = 0.1M line
    x3 = [10.0,25.0,40.0];  
    ay3 = fitnlm(x3,y3,mdlp,beta0);

    % Get fit along C = 2.7M line
    x5 = [5.0,10.0,25.0,40.0,45.0];
    y5 = [a_C(8),p3,a_C(6),p4,a_C(9)];    %<===============   
    ay4 = fitnlm(x5,y5,mdlp,beta0);

    % Get fit along C = 4.6M line
    x4 = [10.0,25.0,40.0];
    y4 = [a_C(2),p2,a_C(4)];                %<===============
    ay5 = fitnlm(x4,y4,mdlp,beta0);

    yy2 = feval(ay4,allX2);
    yy3 = feval(ay3,allX2);
    yy4 = feval(ay5,allX2);

%     figure(3)
%     hold on
%     plot(x5,y5,'go')
%     plot(allX2,yy2,'-g')
%     plot(x3,y3,'ro')
%     plot(allX2,yy3,'-r')
%     plot(x4,y4,'bo')
%     plot(allX2,yy4,'-b')
%     hold off    

    % Now find the fits for the linear-linear mdl parameters values as a
    % function of Cl- concentration
    z6 = ay3.Coefficients.Estimate;
    z7 = ay4.Coefficients.Estimate;
    z8 = ay5.Coefficients.Estimate;

    x6 = [0.1, 2.7, 4.6];
    y6 = [z6(1),z7(1),z8(1)];
    y7 = [z6(2),z7(2),z8(2)];
    y8 = [z6(3),z7(3),z8(3)];

    ay8 = fitnlm(x6,y6,mdlp,beta0);
    ay9 = fitnlm(x6,y7,mdlp,beta0);
    ay10 = fitnlm(x6,y8,mdlp,beta0);

    yy5 = feval(ay8,2.7);
    yy6 = feval(ay9,2.7);
    yy7 = feval(ay10,allX1);

%     figure(1)
%     hold on
%     plot(x6,y6,'go')
%     plot(x6,y7,'ro')
%     plot(x6,y8,'bo')
%     plot(allX1,yy5,'-g')
%     plot(allX1,yy6,'-r')   
%     plot(allX1,yy7,'-b')
%     hold off

    s1 = ay8.Coefficients.Estimate(1);
    s2 = ay8.Coefficients.Estimate(2);
    s3 = ay8.Coefficients.Estimate(3);
    s4 = ay9.Coefficients.Estimate(1);
    s5 = ay9.Coefficients.Estimate(2);
    s6 = ay9.Coefficients.Estimate(3);
    s7 = ay10.Coefficients.Estimate(1);
    s8 = ay10.Coefficients.Estimate(2);
    s9 = ay10.Coefficients.Estimate(3);
    aLine = strcat(num2str(s1),',',num2str(s2),',',num2str(s3),',',num2str(s4),',',num2str(s5),',',num2str(s6),',',num2str(s7),',',num2str(s8),',',num2str(s9),',','2','\n');
    disp(aLine)
    
    yy5 = feval(ay8,0.6);
    yy6 = feval(ay9,0.6);
    yy7 = feval(ay10,0.6);
    
    val = yy5 + yy6*25 + yy7*25^2;  
%     val = (yy5 + (yy6*25))/(1.0 + (yy7 *25));
    actualVal = 0.20283987201577;
    
    fprintf(fileID,aLine);    
    % ===============================
    % ===============================  
    y1 = [d_C(5),d_C(6),d_C(7)];          %<===============
    aLine = strcat(aOne,'\n');
    fprintf(fileID,aLine);  
    % ===============================
    % End Anodic
    % ===============================
    fclose(fileID);

%     figure(2)
%     hold on
%     plot(x2,y2,'ro')
%     plot(10.0,p3,'bo')
%     plot(40.0,p4,'bo')   
%     plot(allX2,allY2,'-b')
%     hold off    
% 
%     a = fit([temps',concCl'],dGA_H','poly32','Robust','on');
%     z_mdl{2} = a;
%     a = fit([temps',concCl'],a_H','poly32','Robust','on');
%     z_mdl{3} = a;
% 
%     figure(1)
%     plot(z_mdl{3},[temps',concCl'],a_H')
% 
%     a = fit([temps',concCl'],d_H',ft);
%     z_mdl{4} = a;
% 
%     a = fit([temps',concCl'],dGC_O',ft);
%     z_mdl{5} = a;
%     a = fit([temps',concCl'],dGA_O',ft);
%     z_mdl{6} = a;
%     a = fit([temps',concCl'],a_O',ft);
%     z_mdl{7} = a;
%     a = fit([temps',concCl'],d_O',ft);
%     z_mdl{8} = a;
% 
%     a = fit([temps',concCl'],dGC_R',ft);
%     z_mdl{9} = a;
%     a = fit([temps',concCl'],dGA_R',ft);
%     z_mdl{10} = a;
%     a = fit([temps',concCl'],a_R',ft);
%     z_mdl{11} = a;
%     a = fit([temps',concCl'],d_R',ft);
%     z_mdl{12} = a;
% 
%     a = fit([temps',concCl'],dGC_C',ft);
%     z_mdl{13} = a;
%     a = fit([temps',concCl'],dGA_C',ft);
%     z_mdl{14} = a;
%     a = fit([temps',concCl'],a_C',ft);
%     z_mdl{15} = a;
%     a = fit([temps',concCl'],d_C',ft);
%     z_mdl{16} = a;
%    
%     t1 = oxideEffectPotentials(1,:)';
%     a = fit([temps',concCl'],t1,ft);
%     z_mdl2 = a;
%     disp(z_mdl2)
% 
%     a = fit([temps',concCl'],oxideEffectPotentials(2,:)',ft);
%     z_mdl3 = a;
%     disp(z_mdl3)    
% 
% %     disp(z_mdl{1})
% 
%     
%     for i = 1:length(z_mdl)
%         p1 = z_mdl{i}.a;
%         p2 = z_mdl{i}.b;
%         p3 = z_mdl{i}.c;
%         p4 = z_mdl{i}.d;
%         p5 = z_mdl{i}.e;
%         p6 = z_mdl{i}.f;
%         aLine = strcat(num2str(p1),',',num2str(p2),',',num2str(p3),',',num2str(p4),',',num2str(p5),',',num2str(p6),'\n\n');
%         disp(aLine)
%         fprintf(fileID,aLine);
%     end

    
    
%==========================================================================    
    tick_label_size = 16;
    axis_label_size = 18;
    title_label_size = 20;
    axis_line_width = 3;
    font_weight = 'bold';
    plot_line_width = 3;
    plot_line_width_2 = 2;
%==========================================================================    
%     figure(1)
%     hold on
%     plot(z_mdl{1},[temps',concCl'],dGC_H')
%     plot(z_mdl2,[temps',concCl'],t1)
%     axis square
%     box on
%     xlabel('Temperature (K)', 'FontSize', axis_label_size,'FontWeight',font_weight)
%     ylabel('[Cl^-](M)', 'FontSize', axis_label_size,'FontWeight',font_weight)
%     zlabel('dG')
%     ax = gca;
%     ax.FontSize = tick_label_size;
%     ax.FontWeight = font_weight;
%     ax.LineWidth = axis_line_width;
%     hold off      
end

function z = linear_linear(b,x)
     num = b(1) + b(2).*x;
     denom = 1.0 + b(3).*x;
     z = num./denom;
end

function z = poly(b,x)
    z = b(1) + b(2).*x + b(3).*x.^2;
end
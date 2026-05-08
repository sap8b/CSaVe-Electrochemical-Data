function fitVsite2
    clc;
    clear all;

    TC = [25.0, 10.0, 40.0, 5.0, 25.0, 45.0, 5.0, 25.0, 45.0, 10.0, 40.0, 25.0, 40.0]; %C
    c_Cl = [0.01, 0.1, 0.1, 0.6, 0.6, 0.6, 2.7, 2.7, 2.7, 4.6, 4.6, 5.2, 5.2]; %M  
%     oEP_ORR = [-0.30,-0.30,-0.30,-0.30,-0.30,-0.30,-0.28,-0.29,-0.20,-0.28,-0.25,-0.20,-0.20];
    oEP_ORR = [-0.20,-0.30,-0.30,-0.30,-0.30,-0.30,-0.28,-0.29,-0.20,-0.28,-0.19,-0.20,-0.21];

    fo = fitoptions('Method','NonlinearLeastSquares');    
    ft = fittype('a*x*y + b*x^2 + c*y^2 + d*x + e*y + f',...
    'dependent',{'z'},'independent',{'x','y'},...
    'coefficients',{'a','b','c','d','e','f'},'options',fo);
    
    fitsurface=fit([TC',c_Cl'],oEP_ORR', ft,'Normalize','on');
    plot(fitsurface,[TC',c_Cl'],oEP_ORR')

    disp(fitsurface)
end
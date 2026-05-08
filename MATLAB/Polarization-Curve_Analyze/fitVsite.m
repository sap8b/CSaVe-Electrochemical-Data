function fitVsite
    clc;
    clear all;

    TC = [25.0, 10.0, 40.0, 5.0, 25.0, 45.0, 5.0, 25.0, 45.0, 10.0, 40.0, 25.0, 40.0]; %C
    c_Cl = [0.01, 0.1, 0.1, 0.6, 0.6, 0.6, 2.7, 2.7, 2.7, 4.6, 4.6, 5.2, 5.2]; %M  
%     oEP_Rx1 = [-0.30,-0.34,-0.34,-0.32,-0.32,-0.32,-0.34,-0.30,-0.30,-0.32,-0.32,-0.32,-0.30]; 
    oEP_Rx1 = [-0.27,-0.34,-0.34,-0.32,-0.32,-0.32,-0.34,-0.30,-0.30,-0.32,-0.30,-0.32,-0.29];

    fo = fitoptions('Method','NonlinearLeastSquares');    
    ft = fittype('a*x*y + b*x^2 + c*y^2 + d*x + e*y + f',...
    'dependent',{'z'},'independent',{'x','y'},...
    'coefficients',{'a','b','c','d','e','f'},'options',fo);
    
    fitsurface=fit([TC',c_Cl'],oEP_Rx1', ft,'Normalize','on');
    plot(fitsurface,[TC',c_Cl'],oEP_Rx1')

    disp(fitsurface)
end
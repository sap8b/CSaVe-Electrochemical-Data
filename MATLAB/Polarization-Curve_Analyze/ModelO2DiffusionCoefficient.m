function DO2 = ModelO2DiffusionCoefficient(cCl,TK)
    %LoadPolarizationCurve - Loads one or more polarization curves
    %
    %The purpose of this function is to load a polarization curve from
    %the specified file. The file must be a comma separated variable
    %type file.
    %
    % Syntax:  LoadAPolarizationCurve(obj, baseDirectory)
    %
    % Inputs:
    %    baseDirectory - Directory to find the files in neededFileNames
    %
    % Outputs:
    %    Sets class currentData and potentialData
    %
    % Other m-files required: none
    % Subfunctions: none
    % MAT-files required: none
    %
    % See also: 
    %
    %==========================================================================
    % Author:   Steven A. Policastro, Ph.D., Materials Science
    % Center for Corrosion Science and Engineering, U.S. Naval Research
    % Laboratory
    % email address: steven.policastro@nrl.navy.mil  
    % Website: 
    % October 2021; Last revision: 26-Oct-2021
    %==========================================================================
    
    %------------- BEGIN CODE --------------
    
    MO2 = 32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6; 
    phi = 0.2;

    params = [0.193015581, -0.000936823, -3738.145703; ...
        0.586220598, -0.001982362, -0.003767555; ...
        -2058331786, 7380780.538, -725742.0949; ...
        -12341118, 7397.380585, -1024619.196; ...
        -0.082481761, 8.05605E-06, -0.005230993; ...
        -13685.50552, 11.9799009, -0.05822883];

    numModelParameters = size(params,1);

    b = zeros(numModelParameters,1);
    for i = 1:numModelParameters
        b(i,1) = LinearLinear(params(i,:),TK);
    end

    B = b(3,1) + b(4,1).*(TK - 273.15); 
    A = b(2,1); 

    eta2 = b(5,1).*exp(b(6,1)./TK);
    eta1 = 1.0 + A.*sqrt(cCl) + B.*cCl;
    eta = eta2.*eta1;
    
    DO2 = b(1,1).* ((sqrt(phi*MO2).*TK)./((VNaCl.*eta).^0.6));
    
    %------------- END OF CODE --------------
end

function y = LinearLinear(b,x)
    num = b(1) + b(2).*x;
    denom = 1.0 + b(3).*x;
    y = num./denom;
end
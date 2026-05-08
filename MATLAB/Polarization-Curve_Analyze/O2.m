classdef O2
    %O2 contains functions for calculating dissolved oxygen properties
    %   This class contains properties for the dissolved oxygen
    %   concentration, cO2, in an NaCl solution and the diffusion
    %   coefficient, dO2, for the dissolved oxygen.  
    %   The setContants property imports the Constants class.
    %==========================================================================
    % Author:   Steven A. Policastro, Ph.D., Materials Science
    % Center for Corrosion Science and Engineering, U.S. Naval Research
    % Laboratory
    % email address: steven.policastro@nrl.navy.mil  
    % Website: 
    % October 2022; Last revision: 12-Oct-2022
    %==========================================================================
    properties
        setConstants Constants
        dO2 double
        cO2 double
        rO2 double
    end

    methods
        function obj = O2(cCl,T)
            %O2 Constructs an instance of this class
            %   This contructor requires a temperature and Cl-
            %   concentration in (mol/L) as inputs so that values for the
            %   diffusivity, dissolved concentration, and solution
            %   conductivity can be calculated
            obj.setConstants = Constants;
            obj.cO2 = obj.calcConcO2(T,cCl);
            obj.dO2 = obj.calcDiffO2(T,cCl);
            obj.rO2 = obj.calcSolnCond(T,cCl);
        end
    end
    methods (Access = private)
                
        function cO2 = calcConcO2(obj,T,cCl)
                %calcConcO2 Determines the dissolved oxygen concentration
                %in an NaCl solution.
                %   This function requires a temperature value (C) and Cl- 
                %   concentration (mol/L) to calculate the concentration of
                %   dissolved oxygen in the solution.         
                molecular_mass_Cl = obj.setConstants.M_Cl * obj.setConstants.convertGtoKg;
                temperature_k = T + obj.setConstants.convertCtoK; 
                Cl_molality_kg = molecular_mass_Cl * cCl;
                Cl_molality_mg = Cl_molality_kg * obj.setConstants.convertKgtoMg;
        
                a1 = 31820.0;
                b1 = -229.9;
                c1 = -19.12;
                d1 = 0.3081;
    
                a2 = -1409.0;
                b2 = 10.4;
                c2 = 0.8628;
                d2 = -0.0005235;
                
                d3 = 0.07464;
        
                acentric_factor_O2 = 0.022;
                num1 = (a1 * acentric_factor_O2) + a2;
                num2 = (b1 * acentric_factor_O2) + b2;
                denom1 = (c1 * acentric_factor_O2) + c2;
                denom2 = 1.0 + (denom1 * temperature_k);
        
                Ln_H_s_0 = (num1 + (num2 * temperature_k)) / denom2;
        
                num3 = d1 + (d2 * temperature_k);
                denom3 = 1.0 + (d3 * temperature_k);
        
                salinity_factor = 0.001;
                salinity = salinity_factor * Cl_molality_mg;
                exp_term = num3 / denom3 * salinity;
        
                Ln_H_s = Ln_H_s_0 + exp_term;
        
                K_H = exp(Ln_H_s);
                x1 = obj.setConstants.cO2 / K_H;
                
                molecular_mass_O2 = obj.setConstants.M_O2 * obj.setConstants.convertGtoKg; %convert_g_to_kg;
                x1_g_L = x1 * molecular_mass_O2 / obj.setConstants.convertGtoKg; %convert_g_to_kg;
    
                x1_g_cm3 = x1_g_L/obj.setConstants.convertLtoCm3;
                cO2 = x1_g_cm3;        
        end
        
        function DO2 = calcDiffO2(obj,T,cCl)
            %calcDiffO2 Determines the diffusivity of dissolved oxygen in
            %an NaCl solution.
            %   This function requires a temperature value (C) and Cl- 
            %   concentration (mol/L) to calculate the diffusivity of 
            %   dissolved oxygen in an NaCl solution.   

            TK = T + obj.setConstants.convertCtoK; 

            params = [0.193015581, -0.000936823, -3738.145703; ...
                0.586220598, -0.001982362, -0.003767555; ...
                -2058331786, 7380780.538, -725742.0949; ...
                -12341118, 7397.380585, -1024619.196; ...
                -0.082481761, 8.05605E-06, -0.005230993; ...
                -13685.50552, 11.9799009, -0.05822883];
        
            numModelParameters = size(params,1);
        
            b = zeros(numModelParameters,1);
            for i = 1:numModelParameters
                b(i,1) = Constants.LinearLinear(params(i,:),TK);
            end
        
            DO2 = StokesModel2(obj,b,TK,cCl); %cm2/s
        end
        
        function D = StokesModel2(obj,b,TK,cCl)
            %StokesModel2 Determines the diffusivity of dissolved oxygen in
            %an NaCl solution using a Stokes model.
            %   This function requires a temperature value (C), Cl- 
            %   concentration (mol/L), and a set of b-parameters to 
            %   calculate the diffusivity of dissolved oxygen in an NaCl 
            %   solution. 
            phi = 2.6; 
        
            eta0 = b(5).*exp(b(6)./TK);
            B = b(3) + b(4).*(TK - 273.15); 
            A = b(2); 
            eta = eta0.*(1.0 + A.*sqrt(cCl) + B.*cCl);
        
            D = b(1).* ((sqrt(phi*obj.setConstants.M_H2O).*TK)./((obj.setConstants.VO2.*eta).^0.6));
        
        end

        function k = calcSolnCond(obj,T,cCl)
        % solutionConductivity - Calculates the conductivity of a solution
        %
        % Function to calculate the conductivity of an NaCl solution.
        %
        % Syntax:  k = solutionConductivity(T,c)
        %
        % Inputs: 
        % c = solution concentration (M) 
        % T = solution temperature (C)
        %  
        %
        % Outputs: Conductivity (S/m)
        %
        %
        % Other m-files required: none
        % Subfunctions: none
        % MAT-files required: none
        %
        % See also: 
        %
        %==========================================================================
        % Author:   Steve Policastro, Ph.D., Materials Science
        % Center for Corrosion Science and Engineering, U.S. Naval Research
        % Laboratory
        % email address: steven.policastro@nrl.navy.mil  
        % Website: 
        % June 2022; Last revision: 24 June 2022
        %==========================================================================
        
            a11 = 0.0480;
            a12 = 0.0034;
            a21 = 6.7545;
            a22 = 0.2392;
            a31 = 0.2065;
            a32 = 0.0013;
        
            num = (a11 + a12.*T) + ((a21 + a22.*T).*cCl);
            denom = 1.0 + ((a31 + a32.*T).*cCl);
        
            k = num./denom; %S/m
            
        end
    end
end
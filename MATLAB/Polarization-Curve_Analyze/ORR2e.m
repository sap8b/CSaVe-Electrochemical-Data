classdef ORR2e < ElectrochemicalReductionReaction
    %ORR2e Summary of this class goes here
    %   Detailed explanation goes here

    methods
        function obj = ORR2e(nameString, pS, T, c_react, c_prod, vApp, dG, alpha, del, cCl)
            %ORR2e Construct an instance of this class
            %   Detailed explanation goes here
            obj.name = nameString;
            obj.Temperature = T;
            obj.lambda_0 = (obj.c.kb*T)/obj.c.planck_h;           
            obj.dG_cathodic = dG(1);
            obj.dG_anodic = dG(2);
            obj.alpha = alpha;
            obj.plotSymbol = pS;
            obj.cReactants = c_react;
            obj.cProducts = c_prod;            

            c_H_g_cm3 = c_react(2)*obj.c.M_H2/1000; %g/cm3 
            D1 = obj.c.D_H;
            obj.z = obj.c.z_orr;
            iLim1 = -(obj.z*obj.c.F*D1*c_H_g_cm3)/del(1); %A/cm2
            
%                     D2 = Calc_D_O2(obj, obj.Temperature);
            D2 = ModelO2DiffusionCoefficient(cCl,obj.Temperature);

            iLim2 = -(obj.z*obj.c.F*D2*c_react(1))/del(2); %A/cm2

            if abs(iLim1) < abs(iLim2)
                obj.iLim =iLim1;
                obj.diffusionLength = del(1);
                obj.diffusionCoefficient = D1;
            else
                obj.iLim =iLim2;
                obj.diffusionLength = del(2);
                obj.diffusionCoefficient = D2;
            end

            pre_factor = (obj.c.R*obj.Temperature)/(obj.z*obj.c.F); 
            EN_log = log((c_react(1) * c_H_g_cm3^4)/c_prod(1));
            obj.EN = obj.c.e0_orr_acid + (pre_factor * EN_log);  

            obj.i = obj.CalculateCurrent(vApp);
        end

    end
end
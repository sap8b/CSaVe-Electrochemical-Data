function i = ActivationCurrent(objCatModel, vApp, b) 
%ActivationCurrent - Calculate the reduction reaction activation-controlled
%current density
%
% The purpose of this function is to calculate the activation-controlled
% current density for a given electrochemical reduction reaction.
%
% Syntax:  i = ActivationCurrent(objCatModel, vApp, b)
%
% Inputs:
%    objCatModel - object of type ElectrochemicalReductionReaction
%    vApp - Vector of applied potentials
%    b - 2-element vector containing values for the energy barrier for the
%    cathodic reaction and the reaction symmetry parameter, beta
%
% Outputs:
%    i - Activation-controlled current density
%
%    Example
%    iH = ActivationCurrent(objCatModel,xData,testB);
%
% Other m-files required: ElectrochemicalReductionReaction
% Subfunctions: none
% MAT-files required: none
%
% See also: ActivationCurrentAnodic, TransportLimitedCurrent
%
%==========================================================================
% Author:   Steve Policastro, Ph.D., Materials Science
% Center for Corrosion Science and Engineering, U.S. Naval Research
% Laboratory
% email address: steven.policastro@nrl.navy.mil  
% Website: 
% October 2021; Last revision: 19-Oct-2021
%==========================================================================
    L1 = length(b);
    if L1 == 2
        RT = objCatModel.c.R*objCatModel.Temperature;
        zF = objCatModel.z*objCatModel.c.F;
        exp_val = -b(1)/RT;
        exp_term = exp(exp_val);
        i0_Cathodic = (zF*objCatModel.lambda_0) * exp_term;     
        an_eta = vApp - (objCatModel.EN - objCatModel.c.E_SHE_to_SCE);
        
        preFactor1 = -(1-b(2))*zF;
        preFactor2 = preFactor1/RT;
        i = i0_Cathodic.*exp(preFactor2.*an_eta);  
    else
        ME = MException('NotEnoughParameters','ActivationCurrent requires 2 parameters but received %s ',L1);
        throw(ME)        
    end

end
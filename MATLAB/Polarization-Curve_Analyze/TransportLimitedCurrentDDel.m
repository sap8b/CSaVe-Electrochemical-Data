function diLddel = TransportLimitedCurrentDDel(objCatModel, vApp, b)
%TransportLimitedCurrent - Calculate the transport-limited current density
%
% The purpose of this function is to calculate the activation-controlled
% current for a given electrochemical reduction reaction.
%
% Syntax:  iLim = TransportLimitedCurrent(objCatModel, x, b)
%
% Inputs:
%    objCatModel - object of type ElectrochemicalReductionReaction
%    vApp - Vector of applied potentials
%    b - scalar parameter containing an estimate of the diffusion length
%    over which the composition of the reactant diffuses to the
%    metal-electrolyte interface
%
% Outputs:
%    iLim - Transport-limited current density
%
%    Example
%    i0 = TransportLimitedCurrent(objCatModel,xData,B);
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
    if L1 == 1
        diLddel = zeros(size(vApp));
        zFDc = objCatModel.z*objCatModel.c.F*objCatModel.diffusionCoefficient*objCatModel.cReactants(1);
        num = -zFDc;
        diLddel(:) = num/(b(1)^2); %A/cm2        
    else
        ME = MException('NotEnoughParameters','TransportLimitedCurrent requires 1 parameters but received %s ',L1);
        throw(ME)         
    end

end
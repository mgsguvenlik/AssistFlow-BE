using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Enums
{
    public enum WorkFlowActionType : short
    {
        // SR (Service Request) evreleri
        ServiceRequestCreated = 10,
        ServiceRequestUpdated = 11,
        ServiceRequestDeleted = 12,

        // WH (Warehouse) evreleri
        WarehouseSent = 20,
        WarehouseCompleted = 21,
        WarehouseBackToSR = 22,

        // TS (Technical Service) evreleri
        TechnicalServiceSent = 30,
        TechnicalServiceStarted = 31,
        TechnicalServiceFinished = 32,
        TechnicalServiceBackToWH = 33,
        TechnicalServiceBackToSR = 34,

        // Genel WorkFlow
        WorkFlowCreated = 40,
        WorkFlowUpdated = 41,
        WorkFlowCancelled = 42,
        WorkFlowSoftDeleted = 43,
        WorkFlowStepChanged = 44,

        // Kurallar/İstisnalar
        LocationOverrideRequested = 50,
        LocationCheckFailed = 51,

        //Fiyatlama
        PricingPending = 1,
        PricingApproved = 2,
        PricingRejected = 3,
        PricingAwaitingReview = 4,
    }

}

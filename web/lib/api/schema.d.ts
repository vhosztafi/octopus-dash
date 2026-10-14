export interface paths {
    "/health": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/status": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["StatusDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/days/latest-complete": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["LatestDayDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/days/{date}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    date: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DayDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/days/{date}/slots": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    filter?: string;
                };
                header?: never;
                path: {
                    date: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["LedgerDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/days/{date}/slots.csv": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    filter?: string;
                };
                header?: never;
                path: {
                    date: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/weeks/{monday}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    monday: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["WeekDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/powerwall/live": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["PowerwallDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/settings": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DashboardSettings"];
                    };
                };
            };
        };
        put: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["DashboardSettings"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DashboardSettings"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                    };
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionsDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/octopus": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["OctopusCredentials"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionTestDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                    };
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/tesla": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["TeslaCredentials"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionTestDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                    };
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/tesla/authorize": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["TeslaAppCredentials"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["TeslaAuthorizationStart"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/tesla/callback": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["TeslaAuthorizationCallback"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionTestDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/octopus/test": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionTestDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/connections/tesla/test": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ConnectionTestDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tesla/sites": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["EnergySiteDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/sync": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description Accepted */
                202: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["CommandDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/reports/weekly/send": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query: {
                    week: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description Accepted */
                202: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["CommandDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        CommandDto: {
            /** Format: int64 */
            id: number;
            status: string;
        };
        ConnectionDto: {
            provider: string;
            configured: boolean;
            hint: null | string;
            accountNumber: null | string;
            status: string;
        };
        ConnectionsDto: {
            octopus: components["schemas"]["ConnectionDto"];
            tesla: components["schemas"]["ConnectionDto"];
        };
        ConnectionTestDto: {
            ok: boolean;
            message: string;
        };
        CostRowDto: {
            label: string;
            band: string;
            /** Format: double */
            kwh: null | number;
            /** Format: double */
            ratePence: null | number;
            /** Format: double */
            costPence: number;
            perDay: boolean;
        };
        DashboardSettings: {
            priceDispatchAtOffPeak: boolean;
            dispatchAllowanceEnabled: boolean;
            /** Format: int32 */
            dispatchAllowanceSlots: number;
            countScheduledWindowDispatches: boolean;
            nightStart: string;
            nightEnd: string;
            gasUnit: string;
            /** Format: double */
            calorificValue: number;
            /** Format: double */
            peakLimitKwh: number;
            peakAlertEnabled: boolean;
            peakSource: string;
            ignoreDispatch: boolean;
            earlyWarning: boolean;
            renotifyReconciliation: boolean;
            /** Format: double */
            gasDailyLimitPounds: number;
            gasAlertEnabled: boolean;
            notifyPush: boolean;
            notifyEmail: boolean;
            /** Format: int32 */
            cooldownMinutes: number;
            weeklyEnabled: boolean;
            weekDay: string;
            weekTime: string;
            recipients: string;
            includeCosts: boolean;
            includeAlerts: boolean;
            includeGas: boolean;
            includeBattery: boolean;
            attachCsv: boolean;
            /** Format: int32 */
            historyDays: number;
            teslaSiteId: null | string;
            smartFlexDeviceId: null | string;
            pricing: components["schemas"]["PricingSettings"];
        };
        DayDto: {
            date: string;
            sample: boolean;
            complete: boolean;
            /** Format: int32 */
            missingSlots: number;
            /** Format: int32 */
            unpricedSlots: number;
            kpis: components["schemas"]["KpisDto"];
            slots: components["schemas"]["SlotDto"][];
            costByRate: components["schemas"]["CostRowDto"][];
            powerwall: components["schemas"]["PowerwallDto"];
            alerts: components["schemas"]["SlotDto"][];
            warnings: string[];
            settings: components["schemas"]["DashboardSettings"];
        };
        EnergySiteDto: {
            id: string;
            name: string;
        };
        HttpValidationProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number;
            detail?: null | string;
            instance?: null | string;
            errors?: {
                [key: string]: string[];
            };
        };
        KpisDto: {
            /** Format: double */
            totalPence: number;
            /** Format: double */
            electricityPence: number;
            /** Format: double */
            gasPence: number;
            /** Format: double */
            importKwh: number;
            /** Format: double */
            effectiveRatePence: null | number;
            /** Format: double */
            offPeakSharePercent: null | number;
            /** Format: double */
            peakImportKwh: number;
            /** Format: double */
            gasKwh: number;
            /** Format: double */
            batterySavingPence: number;
            /** Format: int32 */
            alertCount: number;
        };
        LatestDayDto: {
            date: null | string;
        };
        LedgerDto: {
            date: string;
            sample: boolean;
            filter: string;
            slots: components["schemas"]["SlotDto"][];
            /** Format: double */
            importKwh: number;
            /** Format: double */
            costPence: number;
            counts: {
                [key: string]: number;
            };
            /** Format: int32 */
            missingSlots: number;
        };
        OctopusCredentials: {
            apiKey: string;
            accountNumber: string;
        };
        PowerwallDto: {
            /** Format: double */
            chargePercent: null | number;
            /** Format: double */
            reservePercent: null | number;
            /** Format: double */
            gridWatts: null | number;
            /** Format: double */
            batteryWatts: null | number;
            /** Format: double */
            loadWatts: null | number;
            /** Format: double */
            solarWatts: null | number;
            syncedUtc: null | string;
            /** Format: double */
            chargedKwh: number;
            /** Format: double */
            dischargedKwh: number;
            /** Format: double */
            savingPence: number;
        };
        PricingSettings: {
            /** @default true */
            priceDispatchAtOffPeak: boolean;
            /** @default false */
            dispatchAllowanceEnabled: boolean;
            /**
             * Format: int32
             * @default 12
             */
            dispatchAllowanceSlots: number;
            /** @default true */
            countScheduledWindowDispatches: boolean;
            /** @default 23:30 */
            nightStart: string;
            /** @default 05:30 */
            nightEnd: string;
        };
        SlotDto: {
            startUtc: string;
            endUtc: string;
            startLocal: string;
            endLocal: string;
            localOffset: string;
            band: string;
            /** Format: double */
            ratePence: null | number;
            rateSource: string;
            /** Format: double */
            kwh: null | number;
            /** Format: double */
            costPence: null | number;
            /** Format: double */
            batteryKwh: null | number;
            /** Format: double */
            houseKwh: null | number;
            overLimit: boolean;
        };
        StatusDto: {
            sample: boolean;
            octopusDataUtc: null | string;
            teslaSyncUtc: null | string;
            pricingUtc: null | string;
            octopusHealth: string;
            teslaHealth: string;
            pricingHealth: string;
        };
        TeslaAppCredentials: {
            clientId: string;
            clientSecret: string;
        };
        TeslaAuthorizationCallback: {
            state: string;
            code: string;
        };
        TeslaAuthorizationStart: {
            state: string;
            authorizationUrl: string;
        };
        TeslaCredentials: {
            refreshToken: string;
            clientId: string;
            clientSecret: null | string;
        };
        WeekAlertDto: {
            date: string;
            time: string;
            /** Format: double */
            kwh: number;
            /** Format: double */
            costPence: number;
        };
        WeekDayDto: {
            date: string;
            name: string;
            /** Format: double */
            offPence: number;
            /** Format: double */
            peakPence: number;
            /** Format: double */
            standingPence: number;
            /** Format: double */
            gasPence: number;
            /** Format: double */
            totalPence: number;
            complete: boolean;
        };
        WeekDto: {
            monday: string;
            sample: boolean;
            complete: boolean;
            days: components["schemas"]["WeekDayDto"][];
            kpis: components["schemas"]["KpisDto"];
            /** Format: double */
            previousTotalPence: null | number;
            /** Format: double */
            differencePence: null | number;
            /** Format: int32 */
            gasOverDays: number;
            alerts: components["schemas"]["WeekAlertDto"][];
            settings: components["schemas"]["DashboardSettings"];
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;

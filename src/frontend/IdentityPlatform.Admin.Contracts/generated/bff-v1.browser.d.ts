export interface paths {
    "/bff/api/v1/applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** List applications through the current browser session */
        get: operations["ListBrowserApplications"];
        put?: never;
        /** Create a managed application and return its generated secret once */
        post: operations["CreateBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/antiforgery": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Acquire a native token for this browser generation's application mutations */
        get: operations["GetApplicationAntiforgeryToken"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read sanitized application details */
        get: operations["GetBrowserApplication"];
        /** Edit application presentation and callbacks using its current strong version */
        put: operations["EditBrowserApplication"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/disable": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Disable new sign-ins and issuance without revoking existing grants */
        post: operations["DisableBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/enable": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Re-enable fresh sign-ins without restoring old attempts or extending credentials */
        post: operations["EnableBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/grant-revocations": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read the latest ten revocation jobs using current permissions */
        get: operations["ListBrowserApplicationGrantRevocations"];
        put?: never;
        /** Confirm durable revocation while keeping the application disabled */
        post: operations["CreateBrowserApplicationGrantRevocation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/grant-revocations/{jobId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read committed revocation progress using current permissions */
        get: operations["GetBrowserApplicationGrantRevocation"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/grant-revocations/{jobId}/continue": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Freshly confirm remaining work in a stopped revocation job */
        post: operations["ContinueBrowserApplicationGrantRevocation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/secret-replacements": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Replace a managed secret once with current credential permission and fresh MFA */
        post: operations["ReplaceBrowserApplicationSecret"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/audiences": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["ListBrowserAudiences"];
        put?: never;
        post: operations["CreateBrowserAudience"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/audiences/antiforgery": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetCatalogueAntiforgeryAudience"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/audiences/deletions/{jobId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetBrowserAudienceDeletion"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/audiences/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetBrowserAudience"];
        put: operations["EditBrowserAudience"];
        post?: never;
        delete: operations["DeleteBrowserAudience"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/scopes": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["ListBrowserScopes"];
        put?: never;
        post: operations["CreateBrowserScope"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/scopes/antiforgery": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetCatalogueAntiforgeryScope"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/scopes/deletions/{jobId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetBrowserScopeDeletion"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/scopes/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetBrowserScope"];
        put: operations["EditBrowserScope"];
        post?: never;
        delete: operations["DeleteBrowserScope"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/session": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read local browser session status without renewal */
        get: operations["ReadBrowserSession"];
        put?: never;
        post?: never;
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
        ApplicationAntiforgeryToken: {
            requestToken: string;
        };
        ApplicationCreate: {
            clientId: string;
            credentialExpiresAt: string;
            displayName: string;
            redirectUris: string[];
        };
        ApplicationCredentialMetadata: {
            /** Format: date-time */
            expiresAt: string;
            /** Format: date-time */
            issuedAt: string;
        };
        ApplicationCredentialOutput: {
            application: components["schemas"]["ApplicationDetails"];
            clientSecret: string;
        };
        ApplicationDetails: {
            clientId: string;
            credential: null | components["schemas"]["ApplicationCredentialMetadata"];
            displayName: null | string;
            editable: boolean;
            enabled: null | boolean;
            /** Format: date-time */
            expiresAt: string;
            id: string;
            profile: null | string;
            redirectUris: string[];
        };
        ApplicationEdit: {
            displayName: string;
            redirectUris: string[];
        };
        ApplicationLifecycle: {
            /** @enum {boolean} */
            confirm: true;
            /** @enum {string} */
            reason: "maintenance" | "security" | "configuration";
        };
        ApplicationPage: {
            /** Format: date-time */
            expiresAt: string;
            hasNextPage: boolean;
            items: components["schemas"]["ApplicationSummary"][];
            /** Format: int32 */
            page: number | string;
            /** Format: int32 */
            pageSize: number | string;
        };
        ApplicationSecretReplacement: {
            confirmed: boolean;
            credentialExpiresAt: string;
        };
        ApplicationSummary: {
            clientId: string;
            clientType: null | string;
            displayName: null | string;
            id: string;
        };
        AudienceDetails: {
            displayName: string;
            enabled: boolean;
            /** Format: date-time */
            expiresAt: string;
            id: string;
            owner: string;
            referenced: boolean;
            resource: string;
            version: string;
        };
        AudienceInput: {
            displayName: string;
            enabled: boolean;
            owner: string;
            resource: string;
        };
        BrowserSessionStatus: {
            authenticated: boolean;
            /** Format: date-time */
            expiresAt?: null | string;
            subject?: null | string;
        };
        CatalogueAntiforgeryToken: {
            requestToken: string;
        };
        CatalogueDeleteInput: {
            /** @enum {boolean} */
            confirmed: true;
            requestKey: string;
        };
        CatalogueDeletionDetails: {
            /** Format: date-time */
            createdAt: string;
            /** Format: date-time */
            expiresAt: string;
            /** @enum {string} */
            failure: "none" | "stale" | "referenced" | "authority" | "expired" | "missing";
            id: string;
            /** @enum {string} */
            kind: "scope" | "audience";
            /** Format: date-time */
            observedAt: string;
            /** @enum {string} */
            state: "queued" | "succeeded" | "rejected";
            targetId: string;
        };
        CataloguePage: {
            /** Format: date-time */
            expiresAt: string;
            hasMore: boolean;
            items: components["schemas"]["CatalogueSummary"][];
            /** Format: int32 */
            page: number | string;
            /** Format: int32 */
            pageSize: number | string;
        };
        CatalogueSummary: {
            displayName: string;
            enabled: boolean;
            id: string;
            name: string;
        };
        GrantAcceptance: {
            job: components["schemas"]["GrantJobView"];
            requestKey: string;
        };
        GrantJobPage: {
            /** Format: date-time */
            expiresAt: string;
            items: components["schemas"]["GrantJobView"][];
        };
        GrantJobView: {
            applicationId: string;
            authorizationCount: string;
            /** Format: date-time */
            createdAt: string;
            /** Format: date-time */
            expiresAt: string;
            /** @enum {string} */
            failure: "none" | "authority" | "target" | "storage" | "clock";
            id: string;
            /** @enum {string} */
            state: "queued" | "processing" | "stopped" | "succeeded";
            tokenCount: string;
            /** Format: date-time */
            updatedAt: string;
            version: string;
        };
        GrantRevocation: {
            /** @enum {boolean} */
            confirm: true;
            jobVersion?: null | string;
            /** @enum {string} */
            reason: "maintenance" | "security" | "configuration";
        };
        ProblemDetails: {
            detail?: null | string;
            instance?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            title?: null | string;
            type?: null | string;
        };
        ScopeDetails: {
            audienceId: null | string;
            consentText: string;
            /** @enum {string} */
            disclosure: "none" | "email";
            displayName: string;
            enabled: boolean;
            /** Format: date-time */
            expiresAt: string;
            id: string;
            /** @enum {string} */
            kind: "identity" | "api";
            name: string;
            referenced: boolean;
            resource: null | string;
            version: string;
        };
        ScopeInput: {
            audienceId: null | string;
            consentText: string;
            /** @enum {string} */
            disclosure: "none" | "email";
            displayName: string;
            enabled: boolean;
            /** @enum {string} */
            kind: "identity" | "api";
            name: string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    ListBrowserApplications: {
        parameters: {
            query?: {
                /** @description Literal case-sensitive client-ID prefix. */
                search?: string;
                page?: number;
                pageSize?: number;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationPage"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationCreate"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationCredentialOutput"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetApplicationAntiforgeryToken: {
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
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationAntiforgeryToken"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserApplication: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    EditBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationEdit"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    DisableBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationLifecycle"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    EnableBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationLifecycle"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListBrowserApplicationGrantRevocations: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantJobPage"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateBrowserApplicationGrantRevocation: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "Idempotency-Key": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["GrantRevocation"];
            };
        };
        responses: {
            /** @description Accepted */
            202: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    Location: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantAcceptance"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserApplicationGrantRevocation: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
                jobId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantJobView"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ContinueBrowserApplicationGrantRevocation: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "Idempotency-Key": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
                jobId: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["GrantRevocation"];
            };
        };
        responses: {
            /** @description Accepted */
            202: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    Location: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["GrantAcceptance"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ReplaceBrowserApplicationSecret: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationSecretReplacement"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationCredentialOutput"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListBrowserAudiences: {
        parameters: {
            query?: {
                search?: string;
                page?: number;
                pageSize?: number;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CataloguePage"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateBrowserAudience: {
        parameters: {
            query?: never;
            header: {
                "X-CSRF-TOKEN": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AudienceInput"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AudienceDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetCatalogueAntiforgeryAudience: {
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
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueAntiforgeryToken"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserAudienceDeletion: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                jobId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueDeletionDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserAudience: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AudienceDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    EditBrowserAudience: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["AudienceInput"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AudienceDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    DeleteBrowserAudience: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CatalogueDeleteInput"];
            };
        };
        responses: {
            /** @description Accepted */
            202: {
                headers: {
                    "Cache-Control": "no-store";
                    Location: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueDeletionDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListBrowserScopes: {
        parameters: {
            query?: {
                search?: string;
                page?: number;
                pageSize?: number;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CataloguePage"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateBrowserScope: {
        parameters: {
            query?: never;
            header: {
                "X-CSRF-TOKEN": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ScopeInput"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScopeDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetCatalogueAntiforgeryScope: {
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
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueAntiforgeryToken"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserScopeDeletion: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                jobId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueDeletionDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserScope: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScopeDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    EditBrowserScope: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ScopeInput"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    "Cache-Control": "no-store";
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ScopeDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    DeleteBrowserScope: {
        parameters: {
            query?: never;
            header: {
                "If-Match": string;
                "X-CSRF-TOKEN": string;
            };
            path: {
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["CatalogueDeleteInput"];
            };
        };
        responses: {
            /** @description Accepted */
            202: {
                headers: {
                    "Cache-Control": "no-store";
                    Location: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CatalogueDeletionDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Failed */
            412: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    "Cache-Control": "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ReadBrowserSession: {
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
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["BrowserSessionStatus"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content?: never;
            };
        };
    };
}

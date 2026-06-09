export interface AuthUser {
  id: string;
  email: string;
  name: string;
  tenantId: string;
  tenantName: string;
}

export interface OidcClaims {
  sub: string;
  email: string;
  name: string;
  tenant_id: string;
  tenant_name: string;
}

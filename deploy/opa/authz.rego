package authz

default allow = false

allow {
    input.tenant_id == input.resource.tenant_id
    input.scopes[_] == "read"
    input.attributes.role == "admin"
}
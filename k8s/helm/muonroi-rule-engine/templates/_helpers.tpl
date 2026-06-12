{{- define "muonroi-rule-engine.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "muonroi-rule-engine.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "muonroi-rule-engine.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "muonroi-rule-engine.labels" -}}
helm.sh/chart: {{ include "muonroi-rule-engine.chart" . }}
app.kubernetes.io/name: {{ include "muonroi-rule-engine.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "muonroi-rule-engine.selectorLabels" -}}
app.kubernetes.io/name: {{ include "muonroi-rule-engine.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "muonroi-rule-engine.postgresqlHost" -}}
{{- printf "%s-postgresql" .Release.Name -}}
{{- end -}}

{{- define "muonroi-rule-engine.redisHost" -}}
{{- if .Values.redis.enabled -}}
{{- printf "%s-redis-master" .Release.Name -}}
{{- else -}}
{{- default "redis" .Values.config.redis.host -}}
{{- end -}}
{{- end -}}

{{- define "muonroi-rule-engine.databaseConnectionString" -}}
{{- if .Values.secrets.databaseConnectionString -}}
{{- .Values.secrets.databaseConnectionString -}}
{{- else if .Values.postgresql.enabled -}}
{{- printf "Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;Pooling=true;SSL Mode=Prefer;" (include "muonroi-rule-engine.postgresqlHost" .) (default "rule_engine" .Values.postgresql.auth.database) (default "ruleengine" .Values.postgresql.auth.username) (default "changeme" .Values.postgresql.auth.password) -}}
{{- else -}}
{{- printf "Host=%s;Port=%v;Database=%s;Username=%s;Password=%s;Pooling=true;SSL Mode=Prefer;" (default "postgres" .Values.config.database.host) (default 5432 .Values.config.database.port) (default "rule_engine" .Values.config.database.name) (default "ruleengine" .Values.config.database.username) (default "changeme" .Values.config.database.password) -}}
{{- end -}}
{{- end -}}

{{- define "muonroi-rule-engine.redisPassword" -}}
{{- if .Values.secrets.redisPassword -}}
{{- .Values.secrets.redisPassword -}}
{{- else if and .Values.redis.enabled .Values.redis.auth.enabled -}}
{{- default "changeme" .Values.redis.auth.password -}}
{{- else -}}
{{- default "" .Values.config.redis.password -}}
{{- end -}}
{{- end -}}

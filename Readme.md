# Sistema de Gestión de Archivos y Metadatos (Backend)

## Descripción
Este es el backend para el sistema de gestión de archivos y metadatos. Esta API está construida utilizando **ASP.NET Core** y se encarga de gestionar usuarios, archivos, metadatos y versiones a través de endpoints RESTful.

## Tecnologías Utilizadas
- **Backend**: ASP.NET Core 9.0
- **Base de Datos**: SQL Server
- **ORM**: ADO.NET
- **Autenticación**: JWT + Identity
- **Almacenamiento de Archivos**: Azure Blob Storage / Local
- **API**: RESTful
- **Control de versiones**: Git

## Funcionalidades Clave
1. **Gestión de Usuarios y Roles**:
   - Registro e inicio de sesión con autenticación JWT.
   - Roles (Administrador, Usuario Estándar, Lector).

2. **Subida y Almacenamiento de Archivos**:
   - Subida de documentos (PDF, Word, Excel, imágenes, etc.).
   - Gestión de metadatos asociados a los archivos.
   - Almacenamiento en **Azure Blob Storage** o en servidor local.

3. **Organización y Búsqueda de Archivos**:
   - Clasificación en carpetas y etiquetas.
   - Búsqueda por nombre, categoría, autor, etc.

4. **Control de Versiones**:
   - Mantenimiento de versiones anteriores de documentos.

5. **Auditoría**:
   - Registro de cambios y acciones sobre documentos.

## Cómo Empezar

### Requisitos Previos
- **.NET Core SDK 7.0 o superior**  
  Puedes descargarlo desde: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### Pasos para Ejecutar el Proyecto
1. **Clona el repositorio**:
   ```bash
   git clone https://github.com/tu-usuario/sistema-gestion-archivos-backend.git
   cd sistema-gestion-archivos-backend

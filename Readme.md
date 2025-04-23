# Sistema de Gestión de Archivos y Metadatos (Backend)

## Descripción
Este es el backend para el sistema de gestión de archivos y metadatos. Esta API está construida utilizando **ASP.NET Core** y se encarga de gestionar usuarios, archivos, metadatos y versiones a través de endpoints RESTful.

## Tecnologías Utilizadas
- **Backend**: ASP.NET Core 9.0
- **Base de Datos**: SQL Server
- **Conexión a la Base de Datos**: ADO.NET
- **Autenticación**: JWT (JSON Web Token)
- **Almacenamiento de Archivos**: Azure Blob Storage / Local Storage
- **API**: RESTful
- **Control de versiones**: Git

## Funcionalidades Clave
1. **Gestión de Usuarios y Roles**:
   - Registro e inicio de sesión con autenticación JWT.
   - Roles (Administrador, Usuario Estándar, Lector).

2. **Subida y Almacenamiento de Archivos**:
   - Subida de documentos (PDF, Word, Excel, imágenes, etc.).
   - Gestión de metadatos asociados a los archivos.
   - Almacenamiento en **Azure Blob Storage** o en almacenamiento local (según la configuración).

3. **Organización y Búsqueda de Archivos**:
   - Clasificación con metadatos y etiquetas.
   - Búsqueda por nombre, categoría, autor, etc.

4. **Auditoría**:
   - Registro de cambios y acciones sobre documentos.

## Cómo Empezar

### Requisitos Previos
- **.NET Core SDK 7.0 o superior**  
  Puedes descargarlo desde: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

- **Azure Blob Storage**				
  Necesitarás conectar el proyecto a un contenedor de Azure Blob Storage. Más detalles sobre cómo configurarlo: [https://azure.microsoft.com/es-es/products/storage/blobs](https://azure.microsoft.com/es-es/products/storage/blobs)

### Pasos para Ejecutar el Proyecto

1. **Clona el repositorio**:
   ```bash
   git clone https://github.com/tu-usuario/sistema-gestion-archivos-backend.git
   cd sistema-gestion-archivos-backend
   ```

2. **Instala las dependencias**:
   ```bash
   dotnet restore
   ```

3. **Configura la cadena de conexión a la base de datos**  
   Asegúrate de tener configurada correctamente tu base de datos **SQL Server**. Puedes hacerlo modificando el archivo `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=SistemaGestionArchivos;User Id=usuario;Password=contraseña;"
   }
   ```

4. **Configura Azure Blob Storage** (opcional)
   Si estás utilizando **Azure Blob Storage**, configura el contenedor y agrega la cadena de conexión en el archivo `appsettings.json`:
   ```json
   "AzureBlobStorage": {
     "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=nombrecuenta;AccountKey=clave;EndpointSuffix=core.windows.net",
     "ContainerName": "documentos"
   }
   ```

5. **Ejecuta la migración de la base de datos**

6. **Inicia la aplicación**:
   ```bash
   dotnet run
   ```

---

## Endpoints Principales

- **POST** `/api/auth/registrar`: Registrar un nuevo usuario.
- **POST** `/api/auth/login`: Obtener el token JWT para autenticación.
- **POST** `/api/documentos/subir`: Subir un archivo.
- **GET** `/api/documentos/detalles/{id}`: Obtener detalles de un archivo.
- **GET** `/api/documentos/descargar/{id}`: Descargar un archivo por ID.
- **DELETE** `/api/documentos/eliminar/{id}`: Eliminar un archivo por ID.

---

## Licencia

Este proyecto está bajo la licencia MIT.

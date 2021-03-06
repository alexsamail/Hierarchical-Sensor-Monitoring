﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using HSMServer.Exceptions;
using NLog;

namespace HSMServer.Configuration
{
    public class ClientCertificateValidator
    {
        private readonly Logger _logger;
        private readonly CertificateManager _certificateManager;
        private readonly TimeSpan _updateInterval;
        private readonly List<string> _certificateThumbprints;
        private DateTime _lastUpdate;
        private object _syncRoot;
        public ClientCertificateValidator(CertificateManager certificateManager)
        {
            _logger = LogManager.GetCurrentClassLogger();
            _syncRoot = new object();
            _lastUpdate = DateTime.MinValue;
            _updateInterval = TimeSpan.FromSeconds(20);
            _certificateManager = certificateManager;
            lock (_syncRoot)
            {
                _certificateThumbprints = new List<string>();
            }
            _logger.Info("ClientCertificateValidator initialized");

            //UpdateCertificates();
        }

        private void UpdateCertificates()
        {
            lock (_syncRoot)
            {
                _certificateThumbprints.Clear();

                _certificateThumbprints.AddRange(_certificateManager.GetUserCertificates().Select(d => d.Certificate.Thumbprint));
            }
        }

        public bool IsValid(X509Certificate2 clientCertificate)
        {
            if (DateTime.Now - _lastUpdate > _updateInterval)
            {
                UpdateCertificates();
                _lastUpdate = DateTime.Now;
            }

            bool isValid;
            lock (_syncRoot)
            {
                isValid = _certificateThumbprints.Contains(clientCertificate.Thumbprint);
            }

            return isValid;
        }
        public void Validate(X509Certificate2 clientCertificate)
        {
            try
            {
                //if (connection.ClientCertificate.Thumbprint == _defaultClientCertificateThumbprint)
                //{
                //    if (!IsDefaultClientForbidden(connection))
                //    {
                //        throw new DefaultClientCertificateRejectedException("Default client certificate for the current address rejected!");
                //    }

                //    return;
                //}

                if (DateTime.Now - _lastUpdate > _updateInterval)
                {
                    UpdateCertificates();
                    _lastUpdate = DateTime.Now;
                }

                bool isCert;
                lock (_syncRoot)
                {
                    isCert = _certificateThumbprints.Contains(clientCertificate.Thumbprint);
                }

                if (isCert)
                {
                    return;
                }

                _logger.Warn($"Rejecting certificate: '{clientCertificate.SubjectName.Name}'");

                throw new UserRejectedException(
                    $"User certificate '{clientCertificate.SubjectName.Name}' is wrong, authorization failed.");
            }
            catch (UserRejectedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"ClientCertificateValidator: validate error = {ex}");
            }
        }
    }
}

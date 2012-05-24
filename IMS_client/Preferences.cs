using System.Collections.Generic;
using System.Xml.Serialization;

namespace IMS_client
{
    public class Preferences
    {
        public string ims_proxy_cscf_hostname { get; set; }
        public int    ims_proxy_cscf_port { get; set; }
        public string ims_realm { get; set; }
        public string ims_public_user_identity { get; set; }
        public string ims_private_user_identity { get; set; }
        public string ims_password { get; set; }
        public string ims_ip_address { get; set; }
        public int    ims_port { get; set; }
        public bool   ims_use_detected_ip { get; set; }
        public string ims_service_route { get; set; }
        public string ims_auth { get; set; }

        public bool   presence_enabled { get; set; }

        public bool   xdms_enabled { get; set; }
        public string xdms_user_name { get; set; }
        public string xdms_password { get; set; }
        public string xdms_server_name { get; set; }
        public int    xdms_server_port { get; set; }
        
        public int videocall_height { get; set; }
        public int videocall_width { get; set; }
        public int videocall_fps { get; set; }
        public int videocall_local_port { get; set; }

        public int audiocall_local_port { get; set; }

        [XmlIgnore]
        public List<string> option_sections;

        [XmlIgnore]
        public Dictionary<string, string> setting_item_types;

        public Preferences()
        {
            InitialiseVariables();
        }

        public Preferences(string pcscfHostname,int pcscfPort,string realm,string publicId,string privateId,string password,bool presenceEnabled)
        {
            InitialiseVariables();

            ims_proxy_cscf_hostname = pcscfHostname;
            ims_proxy_cscf_port = pcscfPort;
            ims_realm = realm;
            ims_public_user_identity = publicId;
            ims_private_user_identity = privateId;
            ims_password = password;
            presence_enabled = presenceEnabled;
        }

        private void InitialiseVariables()
        {
            option_sections = new List<string> {"IMS", "Presence", "XDMS", "VideoCall", "AudioCall"};

            ims_proxy_cscf_hostname = "pcscf.open-ims.test";
            ims_proxy_cscf_port = 4060;
            ims_port = 5060;
            ims_realm = "open-ims.test";
            ims_public_user_identity = "sip:alice@open-ims.test";
            ims_private_user_identity = "alice@open-ims.test";
            ims_password = "alice";
            ims_auth = "AKAv1-MD5";
            presence_enabled = false;
            xdms_enabled = false;
            ims_ip_address = "127.0.0.1";
            ims_use_detected_ip = true;

            videocall_fps = 30;
            videocall_width = 320;
            videocall_height = 240;
            
            setting_item_types = new Dictionary<string, string>
                                     {
                                         {"presence_enabled", "checkbox"},
                                         {"xdms_enabled", "checkbox"},
                                         {"ims_use_detected_ip", "checkbox"},
                                         {"ims_service_route", "hidden"},
                                         {"ims_auth", "combobox"},
                                         {"audiocall_first_codec", "audio_codec_choice"},
                                         {"audiocall_second_codec", "audio_codec_choice"}
                                     };
        }
    }
}

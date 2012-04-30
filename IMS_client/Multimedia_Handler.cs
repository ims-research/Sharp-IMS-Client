using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Gst;
using Gst.GLib;
using System.Windows.Forms.Integration;

namespace IMS_client
{
    public class GstMessageEventArgs : EventArgs
    {
        public string type;
        public string message;

        public GstMessageEventArgs(string type, string message)
        {
            this.type = type;
            this.message = message;
        }
    }

    class Multimedia_Handler
    {
        public event EventHandler<GstMessageEventArgs> Gst_Log_Event;
        Preferences settings;

        Element video_tx_pipeline, video_rx_pipeline, audio_tx_pipeline, audio_rx_pipeline;
        MainLoop loop;
        public Video_Window video_window;

        public Multimedia_Handler(Preferences Settings)
        {
            settings = Settings;
            System.Environment.SetEnvironmentVariable("GST_DEBUG", "*:2");
            //C:\gstreamer-newold\lib\gstreamer-0.10
            //System.Environment.SetEnvironmentVariable("GST_PLUGIN_PATH",  "c:\\gstreamer-newold\\lib\\gstreamer-0.10");
            //System.Environment.SetEnvironmentVariable("PATH", "c:\\gstreamer-newold\\bin;" + System.Environment.GetEnvironmentVariable("PATH"));

            // DISABLED MEDIA WHILE TESTING

            //string[] args2 = { "--gst-debug-level=*:2" };

            //Gst.Application.Init("Sharp_Client", ref args2);
            ////Gst.Application.Init();
            //System.Threading.Thread glib_loop_thread;

            //glib_loop_thread = new System.Threading.Thread(this.Glib_Loop);
            //glib_loop_thread.Start();

            video_window = new Video_Window();
        }


        public void Glib_Loop()
        {
            loop = new MainLoop();
            loop.Run();
        }

        public void Start_Video_Rx(int recv_port,string name)
        {
            Element udp_src, rtp_h263_depayloader, h263_decoder, csp_filter, screen_sink;
            Caps caps;

            video_rx_pipeline = new Pipeline("videorx-pipeline");
            Bin bin = (Bin)video_rx_pipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            video_rx_pipeline.Bus.AddSignalWatch();
            video_rx_pipeline.Bus.Message += delegate(object o, MessageArgs args)
            { BusCall(o, args.Message); };

            udp_src = ElementFactory.Make("udpsrc", "udp_src");
            rtp_h263_depayloader = ElementFactory.Make("rtph263pdepay", "h263_deplayloader");
            h263_decoder = ElementFactory.Make("ffdec_h263", "h263_decoder");
            csp_filter = ElementFactory.Make("ffmpegcolorspace", "csp_filter_rx");

            screen_sink = ElementFactory.Make("dshowvideosink", "video-sink_rx");

            if ((video_rx_pipeline == null) || (udp_src == null) || (rtp_h263_depayloader == null) || (h263_decoder == null) || (csp_filter == null) || (screen_sink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Recieving Video!");
            }
            udp_src["port"] = recv_port;

            bin.Add(udp_src, rtp_h263_depayloader, h263_decoder, csp_filter, screen_sink);
            caps = Gst.Caps.FromString("application/x-rtp,clock-rate=90000,payload=96,encoding-name=H263-1998");

            if (!udp_src.LinkFiltered(rtp_h263_depayloader, caps))
            {
                Console.WriteLine("link failed between udp_src and rtp_h263_depayloader");
            }

            if (!Gst.Element.Link(rtp_h263_depayloader, h263_decoder, csp_filter, screen_sink))
            {
                Console.WriteLine("link failed between rtp_h263_depayloader and screen_sink");
            }

            Gst.Interfaces.XOverlayAdapter xadapter = new Gst.Interfaces.XOverlayAdapter(screen_sink.Handle);
            
            video_window.wfhost2.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate()
                        {
                            video_window.remote_video_label.Content = "Remote Video:" + name;
                            xadapter.XwindowId = (ulong)video_window.video_rx_canvas.Handle;
                        }));

            video_rx_pipeline.SetState(State.Playing);
        }

        public void Start_Video_Tx(string dest_ip, int dest_port)
        {
            video_window.Dispatcher.Invoke(
                   System.Windows.Threading.DispatcherPriority.Normal,
                   new Action(
                       delegate()
                       {
                           video_window.Show();
                       }));

            Element camera_src, screen_sink;
            Element csp_filter, csp_filter2, tee;
            Element h263_encoder, rtp_h263_payloader, udp_sink, udp_queue, screen_queue;
            Caps caps;

            video_tx_pipeline = new Pipeline("videotx-pipeline");
            Bin bin = (Bin)video_tx_pipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            video_tx_pipeline.Bus.AddSignalWatch();
            video_tx_pipeline.Bus.Message += delegate(object o, MessageArgs args)
            { BusCall(o, args.Message); };


            if ((camera_src = ElementFactory.Make("videotestsrc", "video_src_tx")) == null)
            {
                Console.WriteLine("Could not create webcam-source");
            }

            csp_filter = ElementFactory.Make("ffmpegcolorspace", "filter_tx");
            csp_filter2 = ElementFactory.Make("ffmpegcolorspace", "filter2_tx");
            screen_sink = ElementFactory.Make("dshowvideosink", "video-sink_tx");
            tee = ElementFactory.Make("tee", "tee_tx");

            screen_queue = ElementFactory.Make("queue", "screen-queue_tx");
            udp_queue = ElementFactory.Make("queue", "udp-queue_tx");

            h263_encoder = ElementFactory.Make("ffenc_h263p", "ffenc_h263p_tx");
            rtp_h263_payloader = ElementFactory.Make("rtph263ppay", "rtp_payloader_tx");
            udp_sink = ElementFactory.Make("udpsink", "udp_sink_tx");

            if ((video_tx_pipeline == null) || (camera_src == null) || (screen_sink == null) || (csp_filter == null) || (csp_filter2 == null) || (h263_encoder == null) ||
                (rtp_h263_payloader == null) || (udp_sink == null) || (udp_queue == null) || (screen_queue == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements!");
            }
            udp_sink["host"] = dest_ip;
            udp_sink["port"] = dest_port;

            bin.Add(camera_src, screen_sink, csp_filter, csp_filter2, tee, h263_encoder, rtp_h263_payloader, udp_sink, udp_queue, screen_queue);
            caps = Gst.Caps.FromString("video/x-raw-rgb,width=" + settings.videocall_width + ",height=" + settings.videocall_height);

            if (!camera_src.LinkFiltered(tee, caps))
            {
                Console.WriteLine("link failed between camera_src and tee");
            }

            if (!Gst.Element.Link(tee, csp_filter, screen_queue, screen_sink))
            {
                Console.WriteLine("link failed between tee and screen_sink");
            }

            if (!Gst.Element.Link(tee, csp_filter2, udp_queue, h263_encoder, rtp_h263_payloader, udp_sink))
            {
                Console.WriteLine("link failed between tee and udp_sink");
            }

            Gst.Interfaces.XOverlayAdapter xadapter = new Gst.Interfaces.XOverlayAdapter(screen_sink.Handle);
            
            video_window.wfhost1.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate()
                        {
                            video_window.local_video_label.Content = "Local Video: " + settings.ims_public_user_identity;
                            xadapter.XwindowId = (ulong)video_window.video_tx_canvas.Handle;
                        }));

            video_tx_pipeline.SetState(State.Playing);
        }

        public void Start_Audio_Tx(string dest_ip, int dest_port, int codec)
        {
            Element ds_src, audio_convert, audio_resample, encoder, payloader, udp_sink;
            Caps caps;
            string encoder_name, payloader_name;
            switch (codec)
            {
                case 0:
                    encoder_name = "mulawenc";
                    payloader_name = "rtppcmupay";
                    break;
                case 3:
                    encoder_name = "gsmenc";
                    payloader_name = "rtpgsmpay";
                    break;
                case 8:
                    encoder_name = "alawenc";
                    payloader_name = "rtppcmapay";
                    break;
                case 14:
                    encoder_name = "ffenc_mp2";
                    payloader_name = "rtpmpapay";
                    break;
                default:
                    encoder_name = "mulawenc";
                    payloader_name = "rtppcmupay";
                    break;
            }

            audio_tx_pipeline = new Pipeline("audiotx-pipeline");
            Bin bin = (Bin)audio_tx_pipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            audio_tx_pipeline.Bus.AddSignalWatch();
            audio_tx_pipeline.Bus.Message += delegate(object o, MessageArgs args)
            { BusCall(o, args.Message); };

            //ds_src = ElementFactory.Make("dshowaudiosrc", "dshow-audio-in");
            ds_src = ElementFactory.Make("audiotestsrc", "dshow-audio-in");
            audio_convert = ElementFactory.Make("audioconvert", "audio_convert");
            audio_resample = ElementFactory.Make("audioresample", "audio_resample");
            encoder = ElementFactory.Make(encoder_name, encoder_name);

            payloader = ElementFactory.Make(payloader_name, payloader_name);
            udp_sink = ElementFactory.Make("udpsink", "udp_sink");

            if ((ds_src == null) || (audio_convert == null) || (audio_resample == null) || (encoder == null) || (payloader == null) || (udp_sink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Audio Tx pipeline!");
            }

            udp_sink["host"] = dest_ip;
            udp_sink["port"] = dest_port;

            bin.Add(ds_src, audio_convert, audio_resample, encoder, payloader, udp_sink);


            if (!Gst.Element.Link(ds_src, audio_convert, audio_resample, encoder, payloader, udp_sink))
            {
                Console.WriteLine("link failed between ds_src and udp_sink");
            }

            audio_tx_pipeline.SetState(State.Playing);
        }

        public void Start_Audio_Rx(int recv_port, int codec)
        {
            Element udp_src, depayloader, decoder, directsoundsink;
            Element audioresample, audioconvert;
            Caps caps;
            string depayloader_name, decoder_name;
            switch (codec)
            {
                case 0:
                    depayloader_name = "rtppcmudepay";
                    decoder_name = "mulawdec";
                    caps = Gst.Caps.FromString("application/x-rtp,clock-rate=8000,payload=0");
                    break;
                case 3:
                    depayloader_name = "rtpgsmdepay";
                    decoder_name = "gsmdec";
                    caps = Gst.Caps.FromString("application/x-rtp,clock-rate=8000,payload=3");
                    break;
                case 8:
                    depayloader_name = "rtppcmadepay";
                    decoder_name = "alawdec";
                    caps = Gst.Caps.FromString("application/x-rtp,clock-rate=8000,payload=8");
                    break;
                case 14:
                    depayloader_name = "rtpmpadepay";
                    decoder_name = "mad";
                    caps = Gst.Caps.FromString("application/x-rtp,media=(string)audio,clock-rate=(int)90000,encoding-name=(string)MPA,payload=(int)96");
                    break;
                default:
                    depayloader_name = "rtppcmudepay";
                    decoder_name = "mulawdec";
                    caps = Gst.Caps.FromString("application/x-rtp,clock-rate=8000,payload=0");
                    break;
            }

            audio_rx_pipeline = new Pipeline("audiorx-pipeline");
            Bin bin = (Bin)audio_rx_pipeline;
            //bin.Bus.AddWatch(new BusFunc(BusCall));
            audio_rx_pipeline.Bus.AddSignalWatch();
            audio_rx_pipeline.Bus.Message += delegate(object o, MessageArgs args)
            { BusCall(o, args.Message); };


            udp_src = ElementFactory.Make("udpsrc", "udp_src");
            depayloader = ElementFactory.Make(depayloader_name, depayloader_name);
            decoder = ElementFactory.Make(decoder_name, decoder_name);
            audioconvert = ElementFactory.Make("audioconvert", "audioconvert");
            audioresample = ElementFactory.Make("audioresample", "audio_resample");
            directsoundsink = ElementFactory.Make("directsoundsink", "directsoundsink");

            if ((udp_src == null) || (depayloader == null) || (decoder == null) || (audioconvert == null) || (audioresample == null) || (directsoundsink == null))
            {
                MessageBox.Show("Error Creating Gstreamer Elements for Audio Rx pipeline!");
            }


            udp_src["port"] = recv_port;

            bin.Add(udp_src, depayloader, decoder, audioconvert, audioresample, directsoundsink);

            if (!udp_src.LinkFiltered(depayloader, caps))
            {
                Console.WriteLine("link failed between camera_src and tee");
            }


            if (!Gst.Element.Link(depayloader, decoder, audioconvert, audioresample, directsoundsink))
            {
                Console.WriteLine("link failed between udp_src and directsoundsink");
            }

            audio_rx_pipeline.SetState(State.Playing);

        }

        private bool BusCall(object o, Message message)
        {
            if (this.Gst_Log_Event != null)
            {
                GstMessageEventArgs eventargs = new GstMessageEventArgs("type", "message");
                string msg;
                Enum err;
                eventargs.type = message.Type.ToString();
                switch (message.Type)
                {
                    case MessageType.Error:
                        message.ParseError(out err, out msg);
                        eventargs.message = message.Src.Name + ":\n Error - " + err + "\n" + msg;
                        break;
                    case MessageType.Eos:
                        eventargs.message = message.Src.Name + ":\n" + "End of stream reached";
                        break;
                    case MessageType.Warning:
                        message.ParseWarning(out err, out msg);
                        eventargs.message = message.Src.Name + ":\n Warning - " + err + "\n" + msg;
                        break;
                    default:
                        eventargs.message = message.Src.Name + ":\n Default - " + "Entered bus call " + message.Type;
                        break;
                }
                this.Gst_Log_Event(this, eventargs);
            }
            return true;
        }

        public void Stop_Video_Tx()
        {
            if (video_tx_pipeline != null)
            {
                video_tx_pipeline.SetState(State.Null);
                video_tx_pipeline.Dispose();
            }
        }

        public void Stop_Video_Rx()
        {
            if (video_rx_pipeline != null)
            {
                video_rx_pipeline.SetState(State.Null);
                video_rx_pipeline.Dispose();
            }
        }

        public void Stop_Audio_Tx()
        {
            if (audio_tx_pipeline != null)
            {
                audio_tx_pipeline.SetState(State.Null);
                audio_tx_pipeline.Dispose();
            }
        }

        public void Stop_Audio_Rx()
        {
            if (audio_rx_pipeline != null)
            {
                audio_rx_pipeline.SetState(State.Null);
                audio_rx_pipeline.Dispose();
            }
        }


        internal void Stop_Loop()
        {
            loop.Quit();
        }
    }
}

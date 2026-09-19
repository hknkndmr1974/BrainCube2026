#import <AppTrackingTransparency/AppTrackingTransparency.h>

extern "C"
{
    // 0=NotDetermined 1=Restricted 2=Denied 3=Authorized
    int BrainCube_AttGetStatus()
    {
        if (@available(iOS 14, *))
        {
            return (int)[ATTrackingManager trackingAuthorizationStatus];
        }
        return 3;
    }

    void BrainCube_AttRequest()
    {
        if (@available(iOS 14, *))
        {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(
                ATTrackingManagerAuthorizationStatus status) {}];
        }
    }
}

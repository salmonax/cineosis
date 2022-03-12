float getScreenThresh(float layout, float2 tc) {
    float eyeFactor = 0;
    float stX, stY;
    if (layout == 1) { /* SBS, the original */
        if (tc.x > 0.5)
            eyeFactor = 0.5;
        stX = pow(tc.x - (0.25 + eyeFactor), 2)*40; /* was 25. */
        stY = pow(tc.y, 0.6) - 0.35; /* was 1.5, no shift term */
    } else { /* Top-Bottom */
        if (tc.y > 0.5)
            eyeFactor = 0.5;
        stX = pow(tc.x - 0.5, 2)*40; 
        stY = pow(tc.y - eyeFactor, 0.6) - 0.35; 
    }
    return (stX + stY)/2;
}